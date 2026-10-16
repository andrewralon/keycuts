# Prompt: implement the Keycuts UI redesign

Copy everything below the line into the implementing agent.

---

You're the **orchestrator** for implementing the Keycuts UI redesign in the repo at `C:\git\keycuts` (Windows, .NET 10, WPF).
The work is already broken into tickets. Your job is to run them **one at a time, in order**, delegating each ticket to a
subagent. Review and verify each ticket before you move on.

## Read first (yourself, before delegating anything)
1. `docs/tickets/HANDOFF.md`: environment, baseline, **safety rules**, code facts, the execution order, and the per-ticket workflow.
2. `docs/tickets/README.md`: the ticket index and the **Decisions** table. Decisions are final; don't reopen them.
3. Skim every `docs/tickets/KC-*.md` once, so you understand how the tickets connect.
4. The design spec is `Figma Redesign Keycuts UI2/` (a React prototype). It's a visual and interaction reference only. Ignore the older `Figma Redesign Keycuts UI/` folder.

## Setup
- Create and switch to a branch `feature/ui-redesign` off the current branch (`modernize-sdk-style-net10`).
- Commit `docs/tickets/` as the first commit. Leave the `Figma Redesign Keycuts UI*` and `keycuts-prototype` folders untracked; KC-18 decides what happens to them.
- Confirm the baseline: `dotnet build keycuts.sln --no-incremental` should succeed with 27 warnings.
  The user has approved force-closing running `keycuts.GUI` or `keycuts.Batmanager` processes (`Stop-Process -Id <pid>`) if they lock the build.

## Loop: for each ticket in HANDOFF.md section 6, in order (KC-19 is resolved; skip it)
1. **Delegate** the ticket to a fresh subagent with full implementation capability (one subagent per ticket; never run two at once).
   Give it this prompt, filling in the ticket:
   > Implement ticket `docs/tickets/KC-XX-….md` in `C:\git\keycuts`. First read `docs/tickets/HANDOFF.md` (follow its
   > safety rules and section 7 workflow exactly), `docs/tickets/README.md` (Decisions are final), and the ticket. Use
   > `Figma Redesign Keycuts UI2/src/App.tsx` and `src/index.css` as the visual reference. Implement only this ticket's scope.
   > Add unit tests for any logic. Never touch the real PATH, registry, or output folder (`C:\Shortcuts`); use temp folders.
   > Run `dotnet build keycuts.sln`, `dotnet test keycuts.sln`, and (once it exists) `dotnet format keycuts.sln --verify-no-changes`.
   > Warnings must not exceed the baseline. Tick the verified acceptance criteria in the ticket and append `## Implementation notes`
   > (files changed, decisions, deviations, and manual QA still needed). Don't commit. Reply with: summary, changed files, test results,
   > manual-QA list, and any blockers.
   Also pass along anything that earlier tickets' implementation notes say this ticket needs to know, such as renamed classes or seams.
2. **Review** the result yourself. Read the diff (`git --no-pager diff`) against the ticket's scope and acceptance criteria. Check that
   nothing unrelated changed, the safety rules were respected, and no prototype-only behavior (mock data or fake timers) slipped into production code.
3. **Verify** independently: run the build (with `--no-incremental` when you count warnings), the tests, and the format check.
   If anything fails, or the scope is incomplete, send the same subagent a follow-up describing exactly what to fix, then re-verify.
   If it's still failing after two attempts, stop and report to the user with the evidence.
4. **Commit** the ticket: `KC-XX: <ticket title>`, with a short body listing key changes.
5. Move on to the next ticket.

## Rules
- One ticket per subagent and one subagent at a time. Don't merge tickets or skip ahead.
- Don't redo a subagent's work yourself except for trivial fixes. Use follow-ups to the same subagent instead.
- If a ticket is ambiguous or conflicts with the code, choose the most conservative option that satisfies the spec, record it in the
  ticket's implementation notes (and in the README Decisions table if it affects other tickets), and continue. Only ask the user about genuine
  product decisions that can't be safely defaulted.
- Keep the existing behavior that the tickets say to preserve, especially the Explorer right-click "keycut this!" launch (`App.xaml.cs` args), the CLI, and the Settings window.
- Agents can't truly test drag from Explorer or a browser, Narrator, live theme switching, or DPI. Collect every ticket's "manual QA still needed"
  items into the KC-18 manual verification matrix for the user.

## When done
Report to the user:
- The commits created (one per ticket).
- The final build, test, and format results, plus the warning count compared with the baseline of 27.
- Deviations from the tickets.
- The consolidated **manual QA checklist** the user needs to run (from KC-18).
- Any open questions.
