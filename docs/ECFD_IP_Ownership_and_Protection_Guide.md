# ECFD — IP Ownership & Protection Guide

**Document type:** Founder Protection & IP Reference
**Audience:** Ahmed (originator of ECFD), with sections relevant to the wider team
**Purpose:** Explain, in plain terms, what kind of ownership actually exists over a graduation project like ECFD, what Egyptian law and university practice typically say about it, and what concrete, low-effort steps protect your position as the project's originator — without slowing the team down or creating unnecessary conflict.

**Important disclaimer:** This is not legal advice. Egyptian IP law, 6th of October University's specific bylaws, and any signed agreements you have with the university or with STEM International all affect the real answer, and none of those documents were reviewed to produce this guide. Where this document states a legal principle, treat it as a starting point for a conversation with a licensed Egyptian IP lawyer and with your university's IP or research office — not as a settled conclusion. Anywhere this guide says "confirm with the university" or "confirm with a lawyer," that is not a formality — it is the actual next step.

---

# 1. Your Founder/Originator Position

You are the person who:

- Conceived ECFD's specific concept (real-time conversational fraud defense fused across ASR, NLP, and anti-spoofing signals with attack-progression state modeling).
- Drove the evaluation process that got the team from an open-ended search for a viable Egypt/MENA B2B security product to ECFD specifically, including the earlier EG-FinCSF concept.
- Authored or directed the authorship of the core architecture documents (technical architecture, engineering specification, build plan, demo plan, team organization).

That is a real and meaningful position — but it's worth separating clearly what it does and doesn't automatically give you, because "founder of the idea" and "legal owner of the resulting IP" are not the same thing, especially inside a university team project. The rest of this document exists to close that gap deliberately, instead of assuming it closes itself.

**The single most important practical point in this entire guide:** ownership disputes are won or lost by evidence created *before* anyone asks the question, not by memory or by who feels most strongly about it afterward. Everything in Sections 3–4 exists to make sure that evidence exists.

---

# 2. Idea vs. Copyright vs. Software vs. Inventions vs. Startup Equity

These are five different things, and Egyptian law (like most jurisdictions) treats them very differently. Conflating them is the single most common mistake founders make.

## 2.1 The idea itself

Egyptian law, like essentially every legal system, **does not protect ideas as such**. "A system that detects social-engineering fraud in real time during a VoIP call by fusing ASR, NLP, and anti-spoofing signals" is a concept. Concepts aren't copyrightable and aren't patentable on their own — only their concrete expression (copyright) or a specific, novel, non-obvious technical implementation (patent) can be protected. This means:

- Having "come up with ECFD first" matters enormously for team credit, for your resume/LinkedIn narrative, and for any future founder-equity conversation — but it does not, by itself, give you an enforceable legal right against a teammate who builds something similar independently later.
- What *does* become protectable is everything downstream of the idea: the specific written documents, the specific code, the specific trained models, and — if you go that route — a specific patentable technical mechanism.

## 2.2 Copyright (the concept documents, the code, the diagrams)

Egypt's IP framework is unified under **Law No. 82 of 2002 on the Protection of Intellectual Property Rights**, Book Four of which covers copyright and neighboring rights. Key general principles, subject to the caveats in Section 6:

- Copyright protection **arises automatically on creation** of an original work — you do not need to register anything for the protection to exist, though registration with the Egyptian Copyright and Neighboring Rights Society can serve as useful evidence of ownership in a dispute.
- Software (source code) is generally treated as a literary work for copyright purposes in most jurisdictions aligned with the Berne Convention and TRIPS, which Egypt is a signatory to — this typically extends to your architecture documents, the engineering specification, the demo script, and the codebase itself.
- **Moral rights** (the right to be identified as author, and to object to distortion of the work) are, in most Berne-aligned systems including Egypt's, personal to the individual author and not something you can simply sign away, even if economic rights are transferred or shared. This matters later if ECFD is ever commercialized under a different name or by a different team without your involvement.

## 2.3 The software as a product/system

Separate from copyright in the code, there's the question of who has the right to run, modify, sell, or license ECFD *as a system* going forward. This is typically governed not by copyright law directly but by:

- Whatever agreement (formal or informal) the team has about the project (Section 5).
- University policy, if the university claims any rights over student projects (Section 6).
- If ECFD is ever spun into a company, a cap table and founders' agreement (Section 13) — copyright ownership by an individual does not automatically become company ownership; it has to be assigned or licensed to the entity.

## 2.4 Inventions (patents)

If any part of ECFD's approach is genuinely novel and non-obvious at a technical level — for example, a specific method for fusing ASR/NLP/anti-spoof signals into a risk score, or a specific attack-progression state-machine design — that could in principle be patentable subject matter under Book One of Law 82/2002. In practice, for a graduation-project MVP:

- Patent protection is expensive, slow (patent examination takes years), and requires the invention not to have been publicly disclosed before filing in most systems — which directly conflicts with presenting ECFD at a public graduation discussion (Section 12).
- Most graduation projects should **not** default to pursuing a patent. It's worth knowing the option exists and understanding the disclosure trade-off (Section 12), but it shouldn't drive your day-to-day decisions unless a specific, genuinely novel technical mechanism emerges and you deliberately decide the patent route is worth the disclosure trade-off.

## 2.5 Startup equity

This is the most flexible and, realistically, the most important category for your actual goals. Equity is not automatically tied to who wrote the most code or who had the idea first — it's whatever the founders agree to, in writing, when (or before) the company is formed. Egyptian IP law does not decide this for you; a founders' agreement does. See Section 13.

**Summary table:**

| Category | Protects | Arises automatically? | Relevant here |
|---|---|---|---|
| Idea/concept | Nothing directly | N/A | Credit and narrative only |
| Copyright | Docs, code, diagrams, as expressed | Yes, on creation | Your architecture docs and code |
| Patent | A specific novel technical mechanism | No — must file | Only if something truly novel emerges |
| University claim | Varies by institution | Depends on policy | Must confirm — Section 6 |
| Startup equity | Ownership of a future company | No — must be agreed | Section 13 |

---

# 3. How to Establish a Dated Development Record

This is the highest-leverage, lowest-effort thing you can do, and it should start immediately if it hasn't already.

## 3.1 Why dates matter

In almost every real-world dispute about "who came up with what first," the deciding factor is contemporaneous, timestamped evidence — not testimony given months later. You already have a strong head start: the six ECFD documents, the earlier EG-FinCSF exploration, and this handbook were all created with visible authorship and timestamps.

## 3.2 What to do, concretely

- **Keep everything in a system with real timestamps.** Git commit history (Section 9) is excellent for this. So is a private, dated Google Doc revision history, or even dated email-to-self summaries. Screenshots of chat conversations with your team, kept in a dated folder, count too.
- **Author documents under your own name/account**, not a shared anonymous doc. If the team collaborates in a shared file, make sure your specific contributions are attributable (tracked changes, commit authorship, comment history).
- **Don't backdate anything, ever.** A record that looks tampered with is worse than no record — it undermines your credibility on everything else.
- **Keep a simple, running personal log** (a private note, not shared with the team) of key decisions and when you made them: "Aug 2026 — proposed pivot from EG-FinCSF to ECFD after evaluating X, Y, Z with the team," "Aug 2026 — drafted the risk fusion formula and progression state machine." This costs five minutes and is disproportionately useful later.

---

# 4. Contribution Register

A contribution register is a simple, shared, living document that records who did what, so credit doesn't rely on memory six months from now — and so a conflict, if one ever arises, has a factual record to point to rather than competing recollections.

## 4.1 Suggested format

Keep this in `docs/CONTRIBUTIONS.md` in the repository, updated at least at every sprint retro (per the collaboration handbook's weekly workflow):

```text
# ECFD Contribution Register

## Concept & Architecture
- Original concept, MENA market framing, and pivot from EG-FinCSF: Ahmed
- Risk fusion formula (v1): Ahmed / [names]
- Attack progression state machine: [names]
- Database schema v1: [names]

## By Component (updated per sprint)
### Backend
- CallSessionManager: [name] — Sprint 3
- Risk Engine: [name] — Sprint 11
...

### ASR
...

### NLP
...

### Anti-Spoof
...

### Frontend / Dashboard
...

### Data & Evaluation
...
```

## 4.2 Why this benefits everyone, not just you

Framed correctly to the team, this isn't "Ahmed protecting his territory" — it's a practical artifact that:

- Makes individual grading/assessment easier if the course requires demonstrating individual contribution.
- Gives every team member their own evidence trail for their CV and LinkedIn (this is squarely in the team's shared interest — everyone benefits from a clear, dated record of what they built).
- Removes ambiguity if the team ever needs to decide founder equity for a future company (Section 13).

Introduce it early and frame it as standard engineering practice (which it is), not as a defensive move.

---

# 5. Team IP Agreement

## 5.1 Why a short written agreement is worth having

Even a one-page, informal, team-signed document is far better than an unwritten assumption that "we all just know" who owns what. Most student-team IP disputes happen precisely because nobody wrote anything down while everyone was getting along.

## 5.2 What it should cover, minimally

```text
1. Ownership of the codebase and documents during the academic project
   (e.g., jointly owned by all active contributors, in proportion to
   contribution, OR solely owned by the originator with contributors
   retaining rights to their individual authored components — pick one
   explicitly, don't leave it implicit).

2. What happens if ECFD is commercialized after graduation
   (e.g., current team gets right of first refusal on founding roles/
   equity; departure/inactivity terms — see Section 10).

3. What happens if a team member leaves before graduation
   (do they retain any rights to the final work? Typically reasonable:
   credit for contributed components, no ongoing claim on later
   commercial development they didn't participate in).

4. Confidentiality of any non-public elements
   (see Section 8).

5. Acknowledgment that university policy may apply and takes
   precedence to the extent required by law/university rules
   (see Section 6).
```

## 5.3 How to raise this without creating friction

The easiest time to propose this is early, framed as routine hygiene rather than a response to any specific worry — "every real engineering team documents who owns what before it matters, let's do the same" lands very differently from raising it after a disagreement has already started. Given your prior startup experience (Senior Year Memory Capsule, Skileez), you can reasonably present this as standard founder practice rather than something specific to ECFD or to any distrust of the team.

---

# 6. University IP Considerations

This is the section where you most need to verify rather than assume, because it varies by institution and by country, and general knowledge about it can be wrong for your specific case.

## 6.1 The general landscape

Universities' claims over student-created IP fall into a few common patterns internationally:

- **No claim** — the university treats student coursework/project IP as belonging to the student(s), often with an implied or explicit license for the university to keep a copy for archival/evaluation purposes.
- **Shared/negotiated claim** — the university claims rights only if university resources beyond normal instruction were used (e.g., significant lab equipment, funded compute, a paid research assistantship), otherwise the default is student ownership.
- **Institutional claim** — the university claims ownership of work produced as part of a degree program, sometimes with a process for students to request a rights release for commercialization.

## 6.2 What determines which pattern applies to you

- **6th of October University's specific bylaws or student handbook** — this is the actual source of truth, not general assumptions about Egyptian universities. Public university IP policy documents were not reviewed for this guide.
- **Whether ECFD used significant university-provided resources** (funded GPU/compute, university lab space, a paid supervisor grant) versus resources the team obtained itself.
- **Whether you signed anything at enrollment or at graduation-project registration** that referenced IP — many universities include a clause in graduation-project registration paperwork that is easy to sign without reading closely.

## 6.3 What to actually do

1. Ask your department or the university's research/IP office (if one exists) directly: "Does the university claim any ownership or license rights over graduation-project software, and if so, under what conditions, and is there a process to request a release for future commercialization?" Get the answer in writing (email is fine).
2. Check whatever paperwork you signed at graduation-project registration.
3. If the answer is unclear or informal ("we've never really dealt with this"), that's still useful information — it means precedent doesn't exist yet, and getting something in writing now (even an email confirming the informal understanding) is more valuable than it would be at a university with well-established policy.

---

# 7. Supervisor Rights

- A supervising professor's role is normally academic guidance, not co-authorship or co-ownership of the resulting IP, **unless** the supervisor contributed original technical or conceptual work beyond guidance/feedback, or unless a specific agreement or university policy says otherwise.
- If your supervisor secures funding, compute resources, or lab access specifically for ECFD, that can change the analysis — funded research often comes with IP strings attached, and this is exactly the kind of detail to clarify with the supervisor and the university's research office before accepting significant funded resources, not after.
- Keep supervisor feedback and guidance clearly distinguishable in your records (Section 3–4) from student-authored technical contributions, so it's easy to show later which parts of the work were supervisor-directed versus student-originated.
- If you plan to commercialize ECFD after graduation, it's worth having a direct, friendly conversation with your supervisor about that intent well before graduation — professors are frequently supportive of this and it avoids any appearance of concealment later.

---

# 8. Confidentiality and Public Disclosure

## 8.1 The tension you're managing

Graduation projects are inherently public-facing (a discussion/defense in front of examiners, per `05_ECFD_Discussion_Day_Demo.md`), while commercially valuable IP is often more protectable, and more attractive to future investors, if kept confidential until deliberately disclosed.

## 8.2 Practical guidance

- **A university defense is not automatically "public disclosure" in the legal sense** if it's presented to a closed panel of examiners rather than published or made publicly accessible — but if the university publishes a copy of your graduation report/thesis in an open repository, that likely is public disclosure. Confirm with the university whether and how graduation projects are archived/published.
- If you think there's a real chance of pursuing a patent (Section 2.4), avoid public disclosure of the specific novel mechanism until either a patent application is filed or you've deliberately decided not to pursue that route — this is one of the few places where the "protect first, present later" instinct is legally justified rather than just cautious.
- For everything else (the general architecture, the demo, the evaluation results), the stronger academic and career move is usually to be as open as reasonably possible — a public, well-documented graduation project is good evidence of the dated development record from Section 3, and strengthens your resume/LinkedIn narrative rather than weakening it.
- If you do want to keep specific elements confidential (e.g., a risk-fusion refinement you're saving for a future commercial version), say so explicitly to the team and mark it clearly in the documentation rather than assuming everyone will intuit what's meant to stay private.

---

# 9. GitHub as Evidence

Git history is one of the strongest, cheapest forms of dated development evidence available to you, and it's already being generated by the team's normal workflow described in the companion `ECFD_Team_Collaboration_and_GitHub_Handbook.md`.

## 9.1 Why it's strong evidence

- Every commit has an author, a timestamp, and a content diff — a far more granular and harder-to-dispute record than "I remember building that."
- Commit history is very difficult to convincingly fabricate after the fact, especially if the repository is hosted on GitHub (which independently timestamps pushes) rather than only existing as local, easily-rewritten history.

## 9.2 How to make sure it actually protects you

- **Commit under your real, verified GitHub account**, not a shared or generic one.
- **Follow the commit conventions in the collaboration handbook** (Section 5 there) — clear, attributable, one-change-per-commit messages make the history much more legible as evidence later, not just easier to work with day to day.
- **Don't squash-merge away individual authorship** if it can be avoided — squashing a PR from three contributors into one commit under one name erases exactly the attribution record you want to preserve. Prefer merge commits or rebase-and-merge that preserve individual commit authorship where practical.
- **Keep the repository private but with the full team as collaborators** (Section 1.2 of the collaboration handbook) rather than a public repo during active development, unless the team has deliberately decided otherwise per Section 8 above.
- **Tag milestones** (Section 15 of the collaboration handbook) — a tagged `v0.1.0` with a dated release note is a clean, citable checkpoint of "this is what existed and worked as of this date."

---

# 10. What to Do If Someone Leaves or Copies the Project

## 10.1 If a team member leaves before graduation

1. Refer to the team IP agreement if one exists (Section 5) — this is exactly the scenario it should already answer.
2. If no agreement exists, have a direct conversation as early as possible about what happens to their contributed components — most reasonable outcomes give them credit/attribution for what they built, without an ongoing claim on work done after they leave.
3. Update the contribution register (Section 4) to reflect the departure and the cutoff point of their involvement.

## 10.2 If you believe someone has copied the project

1. **Gather your dated evidence first** (Sections 3, 4, 9) before confronting anyone — a specific, evidenced timeline is far more persuasive and far less likely to escalate unnecessarily than a general accusation.
2. Distinguish clearly between "copied the idea" (very hard to act on legally, per Section 2.1) and "copied specific code, documents, or diagrams" (a genuine copyright matter, since your original authored expression is protected automatically per Section 2.2).
3. For actual copying of your specific authored materials, options generally include: a direct request to stop/take down, a formal notice (a lawyer-drafted cease-and-desist is more effective than an informal one), and, for online platforms, a copyright takedown request (e.g., GitHub's DMCA-style process, which many platforms honor even outside the US for clear-cut copying).
4. Loop in your university if the other party is also a student or affiliated with the university — academic integrity processes are often faster and lower-friction than legal action for this kind of dispute.
5. This is a genuine case where "talk to a lawyer" is the right first move rather than a formality — even a single paid consultation with an Egyptian IP lawyer, armed with your dated evidence, will clarify your actual options far better than general guidance can.

## 10.3 If it's ambiguous (parallel, independent development)

Recognize that in a space like fraud-detection/security AI, some conceptual overlap with other teams or startups working on similar problems is likely and not, by itself, evidence of copying. Focus your energy on protecting your specific expression (Section 2.2) and your dated record (Section 3), not on trying to claim the underlying idea space, which — per Section 2.1 — isn't something Egyptian law (or most legal systems) will let you own outright.

---

# 11. What to Ask Your University

A concrete list you can take, as-is, to your department, your supervisor, or the university's research/IP office:

```text
1. Does the university claim any ownership, co-ownership, or license
   rights over software, documents, or datasets created for a
   graduation project?

2. If yes, under what conditions does that claim apply (e.g., only if
   university-funded resources/compute/lab space were used)?

3. Is there a process to request a rights release or non-exclusive
   license-back if the team wants to commercialize the project after
   graduation?

4. Will the graduation project (report, code, or demo) be published
   or archived publicly, and if so, where and under what terms?

5. Does the supervising professor have any IP claim over the project
   by default, or only if they contributed original technical work?

6. Is there any existing university policy specifically covering
   student startups spun out of graduation projects (some universities
   have an entrepreneurship office with a standard process for this)?
```

Get answers in writing (email is sufficient) and keep them with your dated development record (Section 3).

---

# 12. Patent and Publication Considerations

- **Patent-vs-publish is a real trade-off, not a formality.** Filing a patent application generally must happen before public disclosure of the specific novel mechanism in most systems (Egypt's Law 82/2002 patent provisions require novelty, and public disclosure before filing typically destroys novelty). Your graduation discussion, and any published report or demo, is a form of disclosure.
- **For most graduation-project MVPs, the realistic and reasonable choice is: don't pursue a patent, prioritize the academic and career value of an open, well-documented, publicly demonstrated project instead.** This is consistent with `05_ECFD_Discussion_Day_Demo.md`'s emphasis on scientific honesty and reproducibility, which is fundamentally in tension with keeping technical mechanisms confidential.
- **The exception:** if, during development, a specific, genuinely novel technical mechanism emerges that you have real reason to think is patentable and commercially significant (this is a high bar — most engineering choices, even good ones, are not patentable novel inventions), pause and get a preliminary opinion from an Egyptian patent attorney *before* your graduation defense, not after. This is a narrow, deliberate exception, not the default path.
- **Publication (a paper, a public writeup) has the same disclosure implications as patenting** — decide the patent question first if you're genuinely considering it, since publishing first forecloses the patent option in most systems.

---

# 13. Future Startup Ownership

If ECFD (or a successor project) becomes an actual startup after graduation, none of the copyright/university analysis above automatically becomes company ownership — that has to be deliberately constructed.

## 13.1 What needs to happen, in rough order

1. **Confirm you and the team actually own the underlying IP free and clear** — this is exactly what Sections 6 (university) and 5 (team agreement) exist to establish before you build a company on top of it.
2. **Decide who the actual founders of the company are** — not necessarily every graduation-team member; founder status and academic-team membership are different questions, and it's worth having that conversation explicitly and early rather than assuming continuity.
3. **Have each founder formally assign or license their individually-owned IP contributions to the new company entity** (a standard "IP assignment agreement," typically drafted by a lawyer at company formation) — this is the step that actually converts personal copyright ownership into company-owned IP, and it's frequently skipped by first-time founders who assume that "we're a team building this together" is sufficient.
4. **Set up a cap table and vesting schedule** reflecting actual founder contributions and ongoing commitment, informed by (but not identical to) the contribution register from Section 4.
5. **Address graduation-team members who are not becoming company founders** — typically resolved with clear credit/attribution, and sometimes a small equity or advisory arrangement if their contribution was significant, but this should be a deliberate decision, not an unresolved ambiguity that surfaces after the company has value.

## 13.2 A note specific to your situation

Given your existing experience founding Senior Year Memory Capsule and Skileez, you likely already have practical intuition for founder-equity conversations — the main addition this guide makes is: do the IP-assignment and university-clearance steps (13.1.1, 13.1.3) *before* the company has any traction or perceived value, not after. Those conversations are dramatically easier when nothing is on the line yet.

---

# 14. Practical Protection Checklist

A condensed, actionable version of everything above.

```text
NOW (do this regardless of how ECFD turns out)
[ ] Keep the repository private, with real accounts, following the
    commit conventions in the collaboration handbook
[ ] Start (or continue) a private, dated personal log of key decisions
[ ] Create docs/CONTRIBUTIONS.md and update it every sprint
[ ] Email the university/department the questions in Section 11 and
    save the written answers
[ ] Check any paperwork you signed at graduation-project registration
    for IP clauses

SOON (within the next month or two)
[ ] Draft a one-page team IP agreement (Section 5.2) and get the team
    to agree to it explicitly, framed as standard practice
[ ] Have a direct, low-stakes conversation with your supervisor about
    your intent to potentially commercialize after graduation
[ ] Decide, as a team, whether the repository stays private through
    graduation or becomes public, and why

BEFORE ANY PUBLIC DISCLOSURE (demo day, publication, public GitHub)
[ ] Confirm you're not sitting on a genuinely patentable mechanism you'd
    regret disclosing — if in doubt, one consultation with a patent
    attorney is cheap insurance
[ ] Confirm what the university will publish/archive and where

IF COMMERCIALIZATION BECOMES REAL
[ ] Confirm university IP clearance in writing (Section 6.3)
[ ] Get an IP assignment agreement drafted when the company is formed
[ ] Set up a cap table/vesting schedule reflecting actual contribution
[ ] Resolve the position of any graduation-team members not becoming
    company founders, explicitly and in writing

IF A DISPUTE ARISES
[ ] Gather dated evidence first (Sections 3, 4, 9) before confronting
    anyone
[ ] Distinguish "copied the idea" from "copied your specific authored
    work" — only the latter is a clear legal matter
[ ] Consult a licensed Egyptian IP lawyer with your evidence in hand
```

---

# 15. Summary

Being ECFD's originator gives you a strong claim on credit, narrative, and (if the team agrees) a leading role in any future company — but Egyptian law does not automatically protect the underlying idea, and it's genuinely unclear, without checking, what your university's specific policy says about graduation-project IP. The fix for both gaps is the same: build a clean, dated, well-attributed record as you go (Git history, a contribution register, a short team agreement), ask the university the direct questions in Section 11 in writing, and treat "talk to a lawyer" as the real next step rather than a formality if this ever moves from a graduation project toward an actual company or toward a genuine dispute.
