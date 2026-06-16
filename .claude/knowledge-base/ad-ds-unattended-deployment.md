---
name: ad-ds-unattended-deployment
description: Complete guide for unattended AD DS forest promotion via Unattend.xml and DCPROMO
metadata:
  type: knowledge-base
  sources:
    - Microsoft Learn - DCPROMO Answer File Syntax
    - AWS - Active Directory Domain Services on AWS
    - AWS - Design considerations for running AD on EC2
---

# Unattended Active Directory Domain Services (AD DS) Deployment

## Overview

Unattended AD DS promotion requires:
1. **Answer file** (Unattend.xml or DCPROMO answer file) with all configuration parameters
2. **Network configuration** (static IP, DNS pointing to self)
3. **Role installation** (DNS Server, AD-Domain-Services)
4. **Promotion script** executed after prerequisites

**Reference:** [DCPROMO Answer File Syntax - Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/dcpromo)

## Network Prerequisites (Critical)

### 1. Static IP Address (REQUIRED for DNS)

DNS Server cannot start without a static IP address. DHCP will cause immediate failure.

```powershell
# Get current DHCP-assigned config
$adapter = Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -First 1
$config = Get-NetIPConfiguration -InterfaceIndex $adapter.InterfaceIndex
$currentIp = $config.IPv4Address[0].IPAddress
$currentGateway = $config.IPv4DefaultGateway[0].NextHop

# Convert to static
Set-NetIPInterface -InterfaceIndex $adapter.InterfaceIndex -DHCP Disabled
Remove-NetIPAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4 -Confirm:$false
New-NetIPAddress -InterfaceIndex $adapter.InterfaceIndex -IPAddress $currentIp -PrefixLength 24
New-NetRoute -InterfaceIndex $adapter.InterfaceIndex -DestinationPrefix "0.0.0.0/0" -NextHop $currentGateway

# Set DNS to self (critical for DNS service startup)
Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses @("127.0.0.1", "8.8.8.8")
```

### 2. DNS Configuration

DNS must resolve itself during promotion:
- **Primary DNS:** 127.0.0.1 (localhost - the DC being promoted)
- **Secondary DNS:** 8.8.8.8 (external fallback)

Without self-reference, DNS service fails to start with "No static IP" errors.

## DCPROMO Answer File Format

**File location in EC2:** `C:\Windows\System32\dcpromo.answer`

**Minimal example for forest root DC:**

```ini
[DCINSTALL]
; Unattended forest promotion
AutoConfigDNS=1
CreateDNSDelegation=0
DatabasePath="C:\Windows\NTDS"
LogPath="C:\Windows\NTDS"
SYSVOLPath="C:\Windows\SYSVOL"
SiteName="Default-First-Site-Name"
InstallDNS=Yes
AllowAnonymousAccess=No
AnswerFile="C:\Windows\System32\dcpromo.answer"

; Forest and domain settings
DomainNetBiosName=CORP
DomainName=corp.local
ForestFunctionality=2012R2
DomainFunctionality=2012R2
ReplicaOrNewDomain=Forest
NewDomainDNSName=corp.local

; Safe Mode Admin password
SafeModeAdminPassword=YourPasswordHere123!
RebootOnCompletion=Yes
```

**Critical parameters:**
- `InstallDNS=Yes` - Installs DNS during promotion
- `AutoConfigDNS=1` - Configures DNS automatically
- `SafeModeAdminPassword` - Directory Services Restore Mode password (REQUIRED)
- `RebootOnCompletion=Yes` - Reboot after successful promotion

**Reference:** [Installing a New Forest Using Answer File - Microsoft Learn](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2008-r2-and-2008/cc770303)

## Installation Sequence (Order Matters)

```powershell
# 1. Configure static IP first
Set-NetIPInterface -DHCP Disabled
# ... (see Network Prerequisites above)

# 2. Install DNS Server role
Install-WindowsFeature DNS -IncludeManagementTools

# 3. Install AD-Domain-Services role
Install-WindowsFeature AD-Domain-Services -IncludeManagementTools

# 4. Create DCPROMO answer file dynamically
$answerFile = @"
[DCINSTALL]
AutoConfigDNS=1
DatabasePath="C:\Windows\NTDS"
LogPath="C:\Windows\NTDS"
SYSVOLPath="C:\Windows\SYSVOL"
SiteName="Default-First-Site-Name"
InstallDNS=Yes
AllowAnonymousAccess=No
DomainNetBiosName=CORP
DomainName=corp.local
ForestFunctionality=2012R2
DomainFunctionality=2012R2
ReplicaOrNewDomain=Forest
NewDNSName=corp.local
SafeModeAdminPassword=Password123!
RebootOnCompletion=Yes
"@
$answerFile | Set-Content -Path "C:\Windows\System32\dcpromo.answer" -Encoding ASCII

# 5. Run DCPROMO with answer file
dcpromo.exe /answer:"C:\Windows\System32\dcpromo.answer" /unattend
```

## AWS EC2 Considerations

**Reference:** [Active Directory Domain Services on AWS - AWS Whitepapers](https://aws.amazon.com/whitepapers/active-directory-domain-services/)

### Security Groups

Allow these ports for AD DS:
- **TCP/UDP 53** - DNS
- **TCP/UDP 88** - Kerberos
- **TCP/UDP 389** - LDAP
- **TCP 636** - LDAPS
- **TCP 3389** - RDP (for troubleshooting)
- **TCP 445** - SMB (replication)
- **UDP 123** - NTP

### EC2 UserData Execution

UserData runs as SYSTEM with full privileges:
```powershell
<powershell>
# Code runs as SYSTEM
# Can execute any privileged operations
</powershell>
```

**Key points:**
- Execution is asynchronous (starts in background)
- Monitor progress via `C:\ProgramData\Amazon\EC2Launch\log\agent.log`
- Can take 20-30 minutes total (network config + role install + promotion + reboot)

### IAM Instance Profile Permissions

If promoting via AWS Systems Manager, instance needs:
- `ssm:GetDocument`
- `ssm:StartAutomationExecution`
- `ec2:DescribeInstances`
- `ec2messages:*`

## Common Errors and Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| "DNS Server Error: No static IP" | Interface using DHCP | Set `Set-NetIPInterface -DHCP Disabled` first |
| "Install-ADDSForest: Cannot validate domain" | DNS not responding | Verify DNS started and self-reference (127.0.0.1) is set |
| "DCPROMO: File not found" | Answer file path wrong | Use full path: `C:\Windows\System32\dcpromo.answer` |
| "Replication Issues" | DNS resolution failing | Ensure all DNS forwarders configured, test with `nslookup` |
| "The specified domain either does not exist or could not be contacted" | Network isolation | Check security group allows LDAP/DNS ports |

## Verification Commands (Post-Promotion)

```powershell
# Verify DC promotion
Get-ADDomain
Get-ADForest
Get-ADDomainController

# Check DNS
nslookup corp.local
Get-Service DNS

# Verify replication
repadmin /replsummary
```

## References

- [DCPROMO Answer File Syntax](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/dcpromo)
- [Installing a New Forest Using Answer File](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2008-r2-and-2008/cc770303)
- [Active Directory Domain Services on AWS](https://aws-solutions-library-samples.github.io/cfn-ps-microsoft-activedirectory/)
- [AD on AWS EC2 Design Considerations](https://docs.aws.amazon.com/whitepapers/latest/active-directory-domain-services/design-considerations-for-running-active-directory-on-ec2-instances.html)
- [AWS Directory Service for AD Trusts](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/microsoftadtrusttep1.html)

