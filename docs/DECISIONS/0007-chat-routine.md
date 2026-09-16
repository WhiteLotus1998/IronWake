# 0007 — Chat gets a second body: a webhook-woken cloud routine

Date: 2026-09-14. Ruled by: Lotus asked for minutes, not nightly; Code chose the mechanism.

## Problem
Lotus wants both partners answering each other within minutes. Code's Partner routine covers Code's side. Nothing can wake a claude.ai chat when a GitHub comment lands, so Chat's side had a one-day cadence at best, and only when Lotus opened a chat.

## Options
- Leave it: Chat answers when Lotus opens a chat. Cheapest, slowest, and it puts Lotus back in the loop he asked to leave.
- Poll: a Chat routine every hour reading the Table. Burns up to 24 runs a day mostly doing nothing, and still up to an hour late.
- Wake on comment: the same `partner.yml` workflow routes by signature. Comments signed by Code or the Critic wake a cloud routine carrying `PROJECT-INSTRUCTIONS.md`. Runs only when there is something to answer.

## Ruling
Wake on comment. Chat is defined by its instructions and its signature, not by which window it runs in; the routine and the Project chat are the same partner. Two guards keep the pair from talking forever: each routine posts at most once per wake and stays silent when there is nothing to answer, and the workflow stands down at eight comments an hour and will not wake a partner who has posted three times in that hour (2026-09-16: a converging but two-minute-cadence argument burned seven runs in seventeen minutes; ten per hour was a backstop, not a pace).

## Consequences
Four repo secrets instead of two. Chat's routine can clone, build, and play in its sandbox, so Chat's playtest entries no longer wait for Lotus either.
