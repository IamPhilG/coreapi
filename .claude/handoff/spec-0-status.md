---
name: spec-0-handoff
date: 2026-06-16
status: RESOLVED (2026-07-16)
assigned-to: next-session
---

# Spec 0 Handoff — EC2 AD DS Auto-Provisioner

## Resolution (2026-07-16)

Option 4 (SSM RunCommand) from the recovery plan below is confirmed working end-to-end:
EC2 launch → IAM instance profile → SSM agent online → AD DS promotion via
`AWS-RunPowerShellScript` → LDAP port 389 → credential bind verification. Both
`DirectoryConnectionTests` integration tests pass against a freshly promoted DC.

Two fixes were needed beyond what's described below:

1. **SSM status polling never progressed** (see "SSM RunCommand approach also failed"):
   root cause was sending the SSM command before the SSM agent had registered as an
   online managed instance. Fixed by polling `ssm describe-instance-information` for
   `PingStatus=Online` before `send-command`.
2. **LDAP bind format**: a WIP change had switched the bind username from UPN
   (`Administrator@corp.local`) to NetBIOS (`CORP\Administrator`). AD DS simple bind
   (`AuthType.Basic`) does not accept the NetBIOS `DOMAIN\user` form — only a full DN or
   UPN. Reverted to UPN; confirmed via `Get-ADDomain` over SSM (without transmitting the
   password through SSM command history) that the promotion itself was healthy before
   making this fix.

Operating model going forward: instances are provisioned on demand for a QA/integration
run and terminated afterward (Elastic IP released) rather than kept running between
sessions, to avoid idle EC2/EIP cost. For active development against a live DC, provision
one via `tools\setup-test-dc.ps1 -Mode demo` and keep it running for the duration of that
work.

---

## Current Status (original — 2026-06-16, superseded above)

**PAUSED** — Core infrastructure works but UserData script never executes on EC2 instance.

### What's Working ✓
- PowerShell wizard (`setup-test-dc.ps1`) launches EC2 instances successfully
- AWS CLI integration, IAM roles, security groups, key pairs all created properly
- Instance launches, gets public IP, associated with Elastic IP
- Fixture detects instance state and can terminate/restart correctly
- DCPROMO answer file generated with proper quote escaping

### What's NOT Working ✗
- **EC2Launch v2.5.1 cannot execute UserData script**
  - Problem: Base64-encoded `<powershell>...</powershell>` received by EC2Launch v2
  - EC2Launch v2 expects YAML/XML config, not raw PowerShell scripts
  - EC2Launch v2 doesn't auto-decode base64 like older versions did
  - Result: AD DS promotion never starts, LDAP polling times out after 30 min

- **SSM RunCommand approach also failed**
  - Implemented but hangs during status polling
  - May need different IAM permissions or instance state requirements
  - Never got past "Sending SSM command" log message

## Blocked Issue

**Root cause:** Windows Server 2022 Base AMI ships with EC2Launch v2, which requires YAML/XML config files, not legacy PowerShell user-data.

**Two paths forward:**
1. **Use older EC2Launch v1** (if available in region) or custom AMI
2. **Rewrite UserData in EC2Launch v2 YAML format** (untested complexity)
3. **Use different instance setup method** (SSM, CloudFormation, Packer custom AMI)

## Files Modified

### Code Changes (26 commits)
- `tools/setup-test-dc.ps1` — PowerShell wizard (working)
- `AdDcProvisionerFixture.cs` — EC2 lifecycle + SSM attempt (partially working, UserData never executes)
- `AdDcProvisionerOptions.cs` — Configuration model
- `.gitignore` — Added `.ssh/` for key pairs
- Knowledge base docs created

### Key Commits
- `b186197` — clean up IAM trust policy JSON handling
- `77d77a4` — SSM RunCommand attempt (failed)
- `a462037` — added error handling to SSM logging

## Next Steps (Priority Order)

### Option 1: Use EC2Launch v1 (Simplest if available)
```powershell
# In setup-test-dc.ps1, specify --user-data-format legacy
# or find Win2022 AMI with EC2Launch v1 installed
```
**Pro:** Existing PowerShell script works unchanged  
**Con:** May not be available in all regions

### Option 2: Custom AMI with EC2Launch v1 pre-baked
**Pro:** Consistent, no format worries  
**Con:** Time to build & maintain

### Option 3: EC2Launch v2 YAML wrapper
Rewrite UserData as YAML config that EC2Launch v2 understands and executes the PowerShell script.  
**Pro:** No custom AMI needed  
**Con:** Untested, may be complex

### Option 4: Drop UserData entirely, use SSM properly
- Launch bare instance
- Use SSM Document with proper IAM permissions
- Execute promotion script via `aws ssm send-command`
- Requires debugging why SSM status polling hung

## Code State

- **Branch:** `feature/test-infra-provisioner`
- **Commits ahead of main:** 27 (includes all attempts)
- **Tests:** Fail at "Waiting for LDAP" (times out after 30 min, instance never promoted)

## Instance Cleanup Issue

The `TerminateInstanceAsync()` method exists but wasn't being called in recent SSM attempts. Need to restore the termination cleanup logic from earlier commits:
- Check git log for `TerminateInstanceAsync` implementation
- Verify `DisposeAsync` calls it correctly
- Test on next restart

## Knowledge Captured

All learnings documented in:
- `.claude/knowledge-base/ad-ds-unattended-deployment.md` — Correct sequence + Microsoft refs
- `.claude/architecture/spec-0-developer-tooling.md` — Architecture decisions
- `.claude/memory/spec-0-learnings.md` — Anti-patterns + critical insights

**Key learning:** PowerShell UserData on Windows isn't "just pass it to EC2" — EC2Launch version and format matter critically.

## Recommended Recovery Plan

1. **Verify EC2Launch version requirement** — Check if Win2022 AMI can run EC2Launch v1
2. **If v1 available:** revert to simple UserData approach, test 1 full run
3. **If v1 unavailable:** Pick Option 3 (YAML) or Option 4 (SSM + IAM permissions)
4. **Before retry:** kill all old test instances to avoid state conflicts

## Time Investment

- ~4 hours on UserData/EC2Launch debugging (base64, file://, raw strings, formats)
- ~1 hour on SSM RunCommand attempt
- Root cause identified but solution not yet implemented
- Architecture is sound; delivery mechanism is the blocker

---

**Prepared:** 2026-06-16  
**Next action:** Investigate EC2Launch v1 availability or commit to Option 3/4  
**Status:** Ready for handoff — all research complete, decision pending
