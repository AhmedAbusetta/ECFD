# ECFD Setup Guide 1 — Accounts, Keys and the Server

**Goal:** by the end of this guide you have four working accounts, their keys safely in one `.env` file, and an empty Ubuntu server on Azure. Nothing is installed on the server yet; that is Guide 2.

**Time:** about 1–2 hours. **Cost:** $0 (only Claude may later need a few dollars).

**Who does it:** one person (the project lead) creates the accounts. Teammates get keys from them privately, never in a group chat.

Do the steps in this order: Anthropic first, because Test 0 needs only that key.

---

## Step 0 — Before you start

Think of `.env` as the team's key box: every key goes in, and the box never leaves your laptop.

1. In the repo folder, copy the template:
   ```bash
   cp .env.example .env
   ```
2. Check that git will never upload it. This command must print a line mentioning `.gitignore`:
   ```bash
   git check-ignore -v .env
   ```
3. Rules for every key below:
   * Paste it **only** into `.env`.
   * Never put it in a screenshot, WhatsApp, Discord, code, or a commit.
   * If a key leaks, delete it in the provider's dashboard and make a new one. A leaked key is a stolen credit card.

---

## Step 1 — Anthropic Claude (the brain)

1. Go to **console.anthropic.com** and sign up. Use the account you will keep for the whole project.
2. Check **Billing / Credits** for any trial credit. If there is none, add a small amount (e.g. $5). Test 0 costs a few cents; a whole scripted call costs about a cent or two.
3. **Set a spending limit first.** In the console's limits/billing settings, set a low monthly limit (e.g. $10). This is the fuse that stops a bug from emptying your balance.
4. Go to **API Keys → Create Key**. Name it `ecfd-dev`.
5. Copy it **once** (it is shown only once) into `.env`:
   ```
   ANTHROPIC_API_KEY=sk-ant-...
   ```
6. Optional but worth it: apply for the **Anthropic research credit program** for academic projects, naming your supervisor and the university. Claude Campus applications for 2026–27 closed on 12 September 2026, so this is the remaining route.

**Check it works** (Git Bash, from the repo folder). The key is read from `.env` and never printed:
```bash
set -a; source .env; set +a; curl -s https://api.anthropic.com/v1/messages -H "x-api-key: $ANTHROPIC_API_KEY" -H "anthropic-version: 2023-06-01" -H "content-type: application/json" -d '{"model":"'"$ANTHROPIC_MODEL"'","max_tokens":30,"messages":[{"role":"user","content":"قول أهلا في كلمة واحدة"}]}'
```
✅ Pass: the reply contains `"type":"message"` and some Arabic text.
❌ `authentication_error`: the key was copied wrong. ❌ `credit balance is too low`: add credit (step 2).

---

## Step 2 — Speechmatics (speech to text)

1. Go to **portal.speechmatics.com** and sign up. No credit card needed.
2. Confirm the free plan on the pricing/usage page. As of July 2026 it reportedly includes about **20 hours of real-time** and 30 hours of batch transcription per month, with 2 calls at once. Write down the exact numbers you see; the plan can change.
3. Go to **API Keys / Manage access → Generate key**. Name it `ecfd-dev`.
4. Copy it into `.env`:
   ```
   SPEECHMATICS_API_KEY=...
   ```

**Check it works** (lists your batch jobs; an empty list is fine):
```bash
set -a; source .env; set +a; curl -s -o /dev/null -w "%{http_code}\n" https://asr.api.speechmatics.com/v2/jobs -H "Authorization: Bearer $SPEECHMATICS_API_KEY"
```
✅ Pass: prints `200`. ❌ `401`: wrong key.

Keep an eye on the usage page: 20 hours is roughly 400 three-minute test calls, but a forgotten open stream keeps counting.

---

## Step 3 — ElevenLabs Scribe (the backup ear)

This account is only for the **one-time swap rule** (if Speechmatics keyword recall stays below 70%). Create it now so the comparison isn't blocked later; don't use it day to day.

1. Go to **elevenlabs.io** and sign up **with your university email**. Students reportedly get the Creator plan free for 3 months; check the students page (elevenlabs.io/students) for how to verify.
2. The free plan alone is about **30 minutes of transcription per month**, enough for the comparison test.
3. **Profile → API Keys → Create**. Name it `ecfd-dev`.
4. Into `.env`:
   ```
   ELEVENLABS_API_KEY=...
   ```

**Check it works:**
```bash
set -a; source .env; set +a; curl -s -o /dev/null -w "%{http_code}\n" https://api.elevenlabs.io/v1/user -H "xi-api-key: $ELEVENLABS_API_KEY"
```
✅ Pass: `200`.

---

## Step 4 — Azure for Students (the server)

Think of this as renting an empty room. Guide 2 puts the phone exchange (Asterisk) in it.

### 4.1 Activate the student subscription
1. Go to **azure.microsoft.com/free/students** and sign up with your **university email**.
2. You get **$100 credit for 12 months, no credit card**. When the credit runs out, things stop instead of billing you — a built-in fuse. You can renew yearly while you are a student.
3. In the portal, search **Subscriptions** and confirm you see "Azure for Students".

### 4.2 Set a budget alert
Search **Cost Management → Budgets → Add**. Budget $20/month, alert at 50% and 90%, sent to your email. This tells you early if something is left running.

### 4.3 Create the VM
Portal → **Virtual machines → Create → Azure virtual machine**:

| Setting | Value | Why |
|---|---|---|
| Resource group | `ecfd-rg` (new) | Deleting this group deletes everything in one click |
| Name | `ecfd-pbx` | |
| Region | One close to Egypt that allows student VMs (e.g. a European region); if a size is unavailable, try another region | Lower delay for live audio |
| Image | **Ubuntu Server 24.04 LTS** | |
| Size | **B1s** (free 750 h/month in year one) to start; move to B2s (4 GB) if Asterisk + tools feel tight | B1s has only 1 GB RAM — fine for Asterisk alone, **not** for any speech model |
| Authentication | **SSH public key**, username `ecfd` | No passwords to guess |
| Key pair | Generate new, name `ecfd-pbx-key`, **download the .pem and keep it out of the repo** | This file is your door key |
| Inbound ports | **SSH (22)** only for now | We open SIP/RTP carefully below |

Click **Review + create → Create**. Copy the **public IP** into `.env`:
```
AZURE_VM_PUBLIC_IP=x.x.x.x
```

### 4.4 Open the network ports — only to your own phones
Live phones talk SIP (signalling) and RTP (voice). If you open these to the whole internet, robots will find your server within hours and try to guess passwords. So we open them **only to the IP addresses of the people testing**.

1. Find your home/mobile public IP: search "what is my ip".
2. VM → **Networking → Add inbound port rule**, twice:

| Name | Port | Protocol | Source |
|---|---|---|---|
| `sip` | 5060 | UDP | IP addresses → your IP(s), comma-separated |
| `rtp` | 10000-10100 | UDP | same IP(s) |

3. Also restrict the existing **SSH (22)** rule's source to your IP(s).
4. Port 4000 (the audio tap) is **not** opened: it stays inside the server on 127.0.0.1.

When your home IP changes (mobile data, a different Wi-Fi), update these rules.

### 4.5 First login
Git Bash on your laptop (Windows has `ssh` built in):
```bash
chmod 600 ~/Downloads/ecfd-pbx-key.pem
ssh -i ~/Downloads/ecfd-pbx-key.pem ecfd@<AZURE_VM_PUBLIC_IP>
```
✅ Pass: you see `ecfd@ecfd-pbx:~$`. Then update the server once:
```bash
sudo apt update && sudo apt -y upgrade
```
Type `exit` to leave.

### 4.6 Stop it when you are not testing
In the portal press **Stop** on the VM when the team is done for the day. A stopped (deallocated) VM costs almost nothing; a running one eats free hours and credit.

---

## Step 5 — Final checklist

| # | Check | Done |
|---|---|---|
| 1 | `.env` exists and `git check-ignore -v .env` prints a line | ☐ |
| 2 | Anthropic test returns Arabic text; monthly spend limit set | ☐ |
| 3 | Speechmatics check returns `200`; free-tier numbers written down | ☐ |
| 4 | ElevenLabs check returns `200` | ☐ |
| 5 | Azure student subscription active; budget alert set | ☐ |
| 6 | VM created, SSH works, ports 5060/10000-10100 open **only** to your IPs | ☐ |
| 7 | `.pem` file stored outside the repo | ☐ |

When rows 1–2 are ticked you can already run **Test 0** (the brain on 20 typed sentences). Rows 3–7 are needed for Guide 2 (Asterisk + softphones).

## If something goes wrong
* **Azure says "size not available" / quota error:** pick another region, or another small size (B1ls, B1ms, B2s).
* **Azure student signup rejects your email:** use the university's official address; some universities need you to sign in through their Microsoft account instead.
* **A key leaked:** delete it in that provider's dashboard immediately, create a new one, update `.env`. Do not try to remove it from git history first — revoke first.
