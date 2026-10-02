# Native participant session binding

Bind existing FarmClientSession offers and Godot input to IPlayerSession. The owned local adapter
preserves solo host disposal; the remote adapter owns only its protocol connection. Privileged saved
history verification remains an explicit local facility, never part of the participant interface.

Requests 16/17 add immutable host-bound presentation and coherent participant status/inquiry reads.
The request header remains version1; the new result shapes use version2, preserving existing version1
bytes. Status serializes with the host but advances no step. Views and status are separate publications;
Paused denotes this participant's readiness, not a promise that another player cannot advance time.

Remote startup waits for actual first view, supported presentation and status before returning unready.
A 250ms refresh shares one query slot with explicit actions: one foreground query may wait behind an
active refresh, and no refresh starts ahead of that pending query. Submit/cancel immediately update
cached inquiry; later status carries Waiting/Offered/terminal changes. Disposal joins queries/refresh.
No automatic command retry, reconnect, copied server root, recreated genesis or local save is allowed.

Native qualification must use actual FarmGame input, views and production metadata over the server
connection. A disposable fixture may trust one explicit loopback test certificate, with name checking;
normal --join uses platform TLS trust. Background rendering avoids shared-desktop focus changes and
proves neither OS/device behavior nor human art acceptance. Full multi-client/reconnect and production
gates remain distinct from a first real native connection. All previous tests/specs remain preserved.
