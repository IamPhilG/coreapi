---
name: ad-ds-promotion-guide
description: AD DS Forest Promotion Prerequisites and Configuration
metadata:
  type: knowledge-base
  source: Microsoft Active Directory Docs
---

# Active Directory Domain Services (AD DS) Promotion Guide

## Prerequisites for AD DS Forest Promotion

### Network Configuration (CRITICAL)

**DNS requires a static IP address.** This is non-negotiable.

1. **Static IP Configuration**
   - Set a static IP address on the network interface (cannot be DHCP)
   - Configure gateway and DNS settings
   - Validate connectivity: `ipconfig /all`

2. **DNS Server Configuration**
   - DNS must resolve itself: point to `127.0.0.1` (localhost) first
   - This allows the DC to resolve domain names during promotion
   - Reference: https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/deploy/install-a-windows-server-2022-active-directory-forest

### Installation Order (Exact Sequence Matters)

```powershell
# Step 1: Configure static IP on all network adapters
Get-NetAdapter | Set-NetIPInterface -DHCP Disabled
New-NetIPAddress -InterfaceIndex <index> -IPAddress 10.0.1.10 -PrefixLength 24 -DefaultGateway 10.0.1.1
Set-DnsClientServerAddress -InterfaceIndex <index> -ServerAddresses ("127.0.0.1","8.8.8.8")

# Step 2: Install DNS Server role (required before AD DS)
Install-WindowsFeature DNS -IncludeManagementTools

# Step 3: Install AD-Domain-Services role
Install-WindowsFeature AD-Domain-Services -IncludeManagementTools

# Step 4: Import ADDSDeployment module
Import-Module ADDSDeployment

# Step 5: Promote to Domain Controller
Install-ADDSForest `
  -DomainName "corp.local" `
  -DomainNetbiosName "CORP" `
  -SafeModeAdministratorPassword (ConvertTo-SecureString 'Password123!' -AsPlainText -Force) `
  -NoRebootOnCompletion:$false `
  -Force
```

## Key References

- https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/deploy/install-a-windows-server-2022-active-directory-forest
- https://learn.microsoft.com/en-us/windows-server/networking/technologies/dhcp/dhcp-subnet-options
- https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/deploy/virtual-dc/adds-on-azure-vm

## Common Errors

### "DNS Server Error: No static IP"
**Cause:** Interface is using DHCP or DNS is pointing to external resolver
**Fix:** Set static IP, point DNS to 127.0.0.1 (self), then install DNS

### "Install-ADDSForest: Cannot validate domain"
**Cause:** DNS not responding or not configured correctly
**Fix:** Verify DNS started: `Get-Service DNS` should be Running

### "Replication Issues"
**Cause:** DNS resolution failing during promotion
**Fix:** Ensure all DNS forwarders are configured and test with `nslookup corp.local`

