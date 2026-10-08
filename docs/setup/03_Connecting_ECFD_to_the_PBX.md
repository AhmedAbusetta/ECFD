# ECFD Setup Guide 3 — Connecting ECFD to the PBX

**Goal:** a real call between the two phones shows up live on the ECFD dashboard: transcript per speaker, rules, AI analyst, alerts.

**Before this:** the PBX works on the Mac ([PBX on a Mac guide](ECFD_PBX_on_Mac_Guide.pdf): both phones registered, echo test 600 works, 1002 → 1001 has two-way audio).

**How it works:** the backend watches Asterisk through ARI (port 8088). When a call is answered it asks Asterisk to copy each person's voice to the laptop (caller → UDP 40000, employee → UDP 40001), cuts the audio into sentences at pauses, and runs them through the normal pipeline. The phone call itself is never touched: if ECFD stops, the call carries on.

---

## 1. Same network
The Mac and the laptop running the backend must be on the **same Wi-Fi / hotspot** as the phones.

## 2. On the Mac (PBX)
1. Get the latest code, then restart the PBX (this now opens ARI port 8088 to the network):
   ```bash
   cd ~/ECFD && git pull
   ./telephony/asterisk/mac-setup.sh
   ```
2. If macOS asks to allow incoming connections for Docker, click **Allow**.
3. Send the Mac's IP (the script prints it) to the person running the backend.
4. *(Recommended)* The ARI password in the repo is public. On a shared network, change `password=` in `telephony/asterisk/ari.conf` on the Mac, re-run the script, and share the new password privately. Don't commit it.

## 3. On the laptop (backend)
1. Find the laptop's IP on the network: `ipconfig` → "Wireless LAN adapter Wi-Fi" → **IPv4 Address**.
2. Check the laptop can reach Asterisk (replace `MAC_IP`); you should get JSON back:
   ```bash
   curl -u ecfd_ari_admin:dev_ari_password http://MAC_IP:8088/ari/asterisk/info
   ```
3. Allow the call audio through the Windows firewall (PowerShell **as Administrator**, once):
   ```powershell
   New-NetFirewallRule -DisplayName "ECFD call audio" -Direction Inbound -Protocol UDP -LocalPort 40000-40019 -Action Allow
   ```
4. Add an `Asterisk` block to `backend/ECFD.Api/appsettings.Local.json` (git-ignored):
   ```json
   "Asterisk": {
     "Enabled": true,
     "AriUrl": "http://MAC_IP:8088/ari",
     "MediaHost": "LAPTOP_IP"
   }
   ```
   Add `"AriPassword": "..."` too if it was changed on the Mac.
5. Start `nlp`, `analyst`, `asr` (fallback), `backend-real-ml` and `dashboard`. The backend log must say:
   `Connected to Asterisk ARI. Calls between phones will be tapped automatically.`

## 4. Make a call
1. Open the dashboard (http://localhost:3000).
2. From phone **1002** (caller) dial **1001** (employee) and answer.
3. The dashboard shows the call by itself — no "Start simulated call" needed. Talk normally; each sentence appears after a short pause, with live words while speaking.
4. Hang up: the call ends on the dashboard.

### Spoken warning for the employee
When the call turns critical, the **employee hears a short chime + warning in Egyptian Arabic** (e.g. "تنبيه، المتصل بيطلب كود التحقق، متدّيهوش."). The caller hears nothing. It plays through a whisper Snoop on the employee's leg; each kind (OTP, secret data, payment, remote app, impersonation, pressure, general) plays at most once, and at most 2 warnings per call.
- The sounds are in `telephony/asterisk/sounds` (already mounted in the container; a `git pull` on the PBX machine is enough). Regenerate them after changing a sentence with `python telephony/tools/make_warning_sounds.py`.
- Turn it off with `"WarnEmployee": false` in the `Asterisk` block; change the limit with `"MaxWarningsPerCall"`.

---

## Troubleshooting

| What you see | Fix |
|---|---|
| Log: `Cannot reach Asterisk ARI` | Wrong `MAC_IP`; not on the same network; PBX not restarted after `git pull` (port 8088 still Mac-only); Mac firewall blocking Docker. Test with the `curl` in step 3.2. |
| Log says `Connected`, but no call appears | The call wasn't answered, or the phones aren't 1001/1002. On the Mac: `docker exec ecfd-asterisk asterisk -rx "ari show apps"` must list `ecfd-stasis`. |
| Log: `Tapping CALLER … -> LAPTOP_IP:40000`, but no transcript | Call audio is blocked or sent to the wrong place: check the Windows firewall rule (step 3.3) and that `MediaHost` is the laptop's **current** IP. On the Mac, `docker exec ecfd-asterisk asterisk -rx "core show channels"` should list `UnicastRTP` channels during the call. |
| Log: `Could not tap …` | ARI refused a request — the error text says why (often a wrong password or an old Asterisk config: re-run `mac-setup.sh`). |
| Sentences are cut in the middle, or two sentences are merged | Tune sentence detection in `appsettings.Local.json` without rebuilding, e.g. `"Segmenter": { "SilenceToCloseMs": 1200, "MinSpeechRms": 250 }` inside the `Asterisk` block. Louder line noise → raise `MinSpeechRms`; soft voices cut off → lower it. |
| Speakers swapped | Only extensions listed in `Asterisk:EmployeeExtensions` (default `1001`) are the employee. |

## Testing without the Mac
`telephony/tools/fake_pbx.py` pretends to be Asterisk and plays recorded clips as a two-sided call. Start `fake-pbx`, then `backend-telephony-local`, with the dashboard open (launch configurations in `.claude/launch.json`).
