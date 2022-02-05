---
uid: uvs_complementary_u_sage
title: Visual Scripting - Complementary Usage
description: How we use visual scripting along with other gamelogic engines
---

# Complementary Usage

So far, we have talked about visual scripting driving the logic of a pinball game. But what if we need to add logic to an *existing* gamelogic engine, without replacing it entirely?

For example, [*Rock (Premier 1978)*](https://www.ipdb.org/machine.cgi?id=1978) isn't entirely emulated in PinMAME; the start-up light sequence is handled by an auxiliary board, and we're only getting control signals, but not signals for every individual light.

In this case, we'd like to keep PinMAME as the driving GLE, but add a visual script that handles the light sequence properly.

## Setup

