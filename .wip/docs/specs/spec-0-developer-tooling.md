---
name: spec-0-developer-tooling
description: Architecture & Design Decisions for Spec 0 - Developer Tooling (EC2 AD DS Auto-Provisioner)
metadata:
  type: architecture
  spec: 0
  status: in-progress
  complexity: high (AWS infrastructure + Windows unattended deployment)
---

# Spec 0 — Developer Tooling: EC2 AD DS Auto-Provisioner

## Objective

Automate the provisioning of a Windows Server 2022 Active Directory Domain Services instance on AWS EC2 for local integration testing and live demos. Developers should be able to run one command and have a fully functional AD DS forest within ~20-30 minutes.

## Architecture Overview

```
Developer Machine
    ↓
tools/setup-test-dc.ps1 (interactive wizard)
    ↓ (creates config)
tests/CoreApi.IntegrationTests/appsettings.Development.json (gitignored)
    ↓ (reads config)
AdDcProvisionerFixture (xUnit IAsyncLifetime)
    ↓ (manages lifecycle)
AWS EC2 Instance (Windows Server 2022)
    ↓ (UserData script)
DCPROMO (unattended forest promotion via answer file)
    ↓
AD DS Forest (corp.local)
    ↓
Integration tests run against real LDAP (port 389)
```

## Components

### 1. `tools/setup-test-dc.ps1` — Interactive Provisioning Wizard

**Purpose:** Collect AWS credentials, region, instance config, and AD domain parameters. Create initial AWS resources.

**Design Decisions:**

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Language | PowerShell 5.1 | Native Windows, AWS CLI via subprocess |
| User interaction | Interactive prompts | Non-technical devs should be able to use it |
| AWS SDK approach | AWS CLI (subprocess) | Avoid .NET AWS SDK versioning issues; easier to debug |
| Idempotency | Yes, safe to re-run | Reusing existing instances, updating config |
| Outputs | `appsettings.Development.json` (gitignored) | Persists configuration between runs |

**Critical Implementation Details:**

1. **AWS CLI Native Execution (Windows):**
   - PowerShell 5.1 uses `CommandLineToArgvW` for native exe args
   - Bare `"` chars are stripped; must escape as `\"` for JSON arguments
   - Solution: `$escaped = $json -replace '"', '\"'`
   - Reference: [unattended-deployment](../../kb/active/ad-ds/unattended-deployment.json)

2. **Idempotent Resource Management:**
   - Query for existing instance + Elastic IP before creating
   - Reuse existing EC2 key pair (or create if missing)
   - IAM role + instance profile created once, reused
   - Security group created once, rules added (safe to re-run)

3. **Error Handling:**
   - `$ErrorActionPreference = "Continue"` for AWS CLI calls (capture errors without dying)
   - Manual exit code checks
   - Clear error messages for missing AWS profile

**User Modes:**

- **Test mode** (default): Instance stops after tests (`-Mode test`)
- **Demo mode**: Instance stays running, AD seeded with demo objects (`-Mode demo`)

### 2. `AdDcProvisionerFixture` — Test Lifecycle Manager

**Purpose:** IAsyncLifetime fixture that orchestrates EC2 instance lifecycle during test execution.

**Design Decisions:**

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Pattern | xUnit `IAsyncLifetime` | Automatic setup/teardown for test collections |
| AWS interaction | AWS CLI via subprocess | Consistent with setup wizard; no SDK version lock |
| Configuration source | `appsettings.Development.json` (gitignored) | Persists state between runs |
| Polling strategy | Exponential backoff? No, fixed 10s intervals | Predictable for timeout calculations |
| Timeout | 30 min (1800s) | DCPROMO + reboot takes 15-25 min; add buffer |
| Progress reporting | Console output with timestamps + 30s heartbeat | Visible to developer during test execution |

**Lifecycle:**

```
InitializeAsync:
  1. Load config from appsettings.Development.json
  2. If ProvisionAdDc=false: skip, use LDAP__* env vars
  3. Start existing instance OR launch new instance
  4. Acquire public IP
  5. Associate Elastic IP (if configured)
  6. Wait for LDAP port 389 responding (poll until timeout)
  7. [Optional] Seed demo data (if SeedDemoData=true)

DisposeAsync:
  1. If test mode: stop instance (keep alive for reuse)
  2. If demo mode: leave running
  3. [Never] terminate (preserve for cost + reuse)
```

**Critical Implementation Details:**

1. **LDAP Readiness Detection:**
   - Polls TCP port 389 (LDAP) until responding
   - Waits for system + instance status checks to pass
   - Timeout: 30 minutes (DCPROMO + reboot)
   - Error if timeout: clear message about promoting vs networking issues

2. **Progress Tracking:**
   - Initial message for each phase: "Launching instance...", "Waiting for LDAP..."
   - Every 30 seconds: elapsed time + timeout countdown
   - Final message: "✓ LDAP ready - AD DS forest online"
   - Helps dev understand what's happening during long waits

3. **Error Messages:**
   - If instance launch fails: show AWS error + troubleshooting steps
   - If LDAP timeout: "AD DS promotion may have failed. Check C:\AddsSetup.log on instance."
   - Never swallow AWS CLI errors

### 3. EC2 UserData — Unattended AD DS Promotion

**Purpose:** Execute on EC2 instance startup. Configure network, install roles, promote to DC.

**Design Decisions:**

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Answer file format | DCPROMO `.ini` (legacy) vs. `Install-ADDSForest` cmdlet | DCPROMO more reliable for unattended; legacy + proven |
| Network config | Static IP (from DHCP) + DNS self-ref (127.0.0.1) | DNS requires static IP; self-ref required for DC promotion |
| Installation sequence | Network → DNS → AD DS → DCPROMO → Reboot | Order matters; DNS fails without static IP |
| Logging | Multiple logs (`C:\AddsSetup.log`, `C:\dcpromo.log`) | Easy debugging if promotion fails |
| Exit handling | Exit code 0 or 3010 (3010 = reboot required) | DCPROMO returns 3010 on success; not an error |

**Sequence:**

```powershell
1. Detect active network adapter (EC2 default)
2. Extract current IP from DHCP (e.g., 10.0.1.42)
3. Disable DHCP, set static IP (10.0.1.42/24)
4. Set DNS to 127.0.0.1 (self) + 8.8.8.8 (fallback)
5. Install DNS Server role
6. Install AD-Domain-Services role
7. Create DCPROMO answer file with parameters:
   - DomainName={{_options.DomainName}}
   - DomainNetBiosName={{_options.DomainNetbiosName}}
   - SafeModeAdministratorPassword={{_options.AdAdminPassword}}
   - RebootOnCompletion=Yes
8. Execute: dcpromo.exe /answer:C:\Windows\System32\dcpromo.answer /unattend
9. Wait for exit (0 or 3010) → automatic reboot
```

**Why This Approach:**

- **DCPROMO answer file** (not raw `Install-ADDSForest` cmdlet):
  - More reliable for unattended promotion
  - Better error handling
  - Clearer logging
  - Industry standard (Microsoft documented)

- **Static IP before DNS:**
  - DNS Server cannot start without static IP
  - Leads to "No static IP address configured" error
  - Must extract DHCP IP and convert to static

- **DNS self-reference (127.0.0.1):**
  - DNS service uses self-reference to resolve domain names
  - Required for DCPROMO to discover SRV records
  - Must point to itself before role installation

## AWS Resource Architecture

### Security Group: `coreapi-test-dc`

Inbound rules (your public IP only):
- TCP/UDP 53 - DNS
- TCP/UDP 88 - Kerberos
- TCP/UDP 389 - LDAP
- TCP 636 - LDAPS
- TCP 3389 - RDP (troubleshooting)
- TCP 445 - SMB (replication)
- UDP 123 - NTP

### IAM Instance Role: `coreapi-test-dc-instance-role`

Policy: `AmazonSSMManagedInstanceCore`
- Allows EC2 Instance Connect (browser-based RDP alternative)
- Allows Systems Manager Session Manager

### EC2 Instance: `coreapi-test-dc`

- AMI: Latest Windows Server 2022 Base (auto-discovered per region)
- Instance type: `t3.medium` (configurable, ~$0.075/hr)
- KeyPair: `coreapi-test-dc-{region}` (for manual RDP if needed)
- [Optional] Elastic IP: Static public IP across start/stop cycles

## Known Issues & Workarounds

### Issue 1: DNS "No Static IP" Error

**Symptom:** DNS role fails to install with error about DHCP.

**Root Cause:** Network interface still using DHCP when DNS is installed.

**Fix:** Set static IP + point DNS to 127.0.0.1 before installing DNS role.

**Reference:** [unattended-deployment - Network Prerequisites](../../kb/active/ad-ds/unattended-deployment.json)

### Issue 2: DCPROMO Hangs or Times Out

**Symptom:** Promotion progresses to ~95% then hangs for 30 min.

**Root Cause:** Usually DNS resolution failing during replication or SRV record checks.

**Debug:** RDP to instance, check:
```powershell
Get-Content C:\dcpromo.log | tail -50
nslookup corp.local
Get-Service DNS
```

**Fix:** Verify DNS started successfully, all DNS forwarders configured, no network isolation.

### Issue 3: Instance Reboot Loop

**Symptom:** Instance keeps rebooting after DCPROMO.

**Root Cause:** DCPROMO restart + Windows Update can chain multiple reboots.

**Workaround:** Increase timeout to 45 min if experiencing this.

### Issue 4: Forgotten or Mismatched AD Administrator Password

**Symptom:** LDAP port 389 responds, but bind fails with "The supplied credential is invalid."

**Root Cause:** The configured `AdAdminPassword` no longer matches the domain Administrator account on the test DC.

**Required response:** Redeploy/recreate the Spec 0 test DC. Do not repair/reset the AD password in place. Spec 0 treats the DC as disposable developer infrastructure; password drift means local configuration and domain state no longer match.

**Fix:** Re-run `tools\setup-test-dc.ps1 -AwsProfile default -Mode test` and choose the fresh/recreate path.

## Testing Strategy

### Unit Tests
- Mock `IDirectoryConnection` for business logic
- No AD connectivity required

### Integration Tests
- Run against real LDAP (port 389)
- Fixture auto-provisions EC2 instance if `ProvisionAdDc=true`
- Tests inherit AD forest + demo objects created by fixture

### Manual Verification
- RDP to instance after promotion complete
- Verify services: `Get-Service AD*, Get-Service DNS, Get-Service LDAP*`
- Verify domain: `Get-ADDomain`
- Verify replication: `repadmin /replsummary`

## Cost Estimate

- **EC2 instance**: ~$2.16/day (t3.medium, 24hr running)
- **Elastic IP**: Free (if instance is running; charged if not associated)
- **Data transfer**: Minimal (within region)
- **Best practice**: Stop instance after test runs (test mode) to avoid charges

**Recommendation:** Use test mode for CI/CD, demo mode for manual demos only.

## Future Enhancements

1. **Terraform version** (separate phase)
   - Define infrastructure as code
   - Store tfstate in S3
   - Replicate across regions

2. **CIS Hardening** (optional)
   - Apply Windows hardening baselines
   - Restrict LDAP anonymous access
   - Enforce LDAPS only

3. **Custom AMI**
   - Pre-bake DNS + AD DS roles
   - Reduce first-launch time from 20 min to 10 min

4. **Multi-region support**
   - Deploy to multiple AWS regions
   - Test federation scenarios

## References

- [unattended-deployment](../../kb/active/ad-ds/unattended-deployment.json) — DCPROMO answer file syntax
- [DCPROMO Answer File - Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/dcpromo)
- [Active Directory on AWS - AWS Whitepapers](https://aws.amazon.com/whitepapers/active-directory-domain-services/)
- [KopiCloud AD API](https://github.com/KopiCloud-AD-API/) — Reference implementation

---

**Document Version:** 1.0  
**Last Updated:** 2026-06-15  
**Status:** In Progress (AD DS promotion working, demo seeding pending)
