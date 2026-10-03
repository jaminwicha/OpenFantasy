# Movement delivery diagnosis

The existing four-player TLS roster test intermittently fails its unchanged five-second movement
observation. The previous checkpoint preserves a fresh failure and a passing traced repeat; neither
pooled tick maxima nor old expired movement samples identify that failed request.

Capture up to three additional unchanged-source/unchanged-binary executions of the original server
selection using the existing opt-in EventSource listener. Stop at the first failed movement case;
otherwise retain three nonreproductions and do not call the failure repaired. Each execution keeps
the original assertions, 150ms movement lease, five-second observation deadline and 600s selection
budget. Execute sequentially under the existing shared heavy-check lock. No other local heavy work
runs alongside these captures. External desktop load remains uncontrolled and must not be assumed
to be causal.

Map diagnostic client/transport/port/host/actor ownership, then follow the first nonzero sample from
client queue through server receipt/admission, first host sampling age, authority commit and delivered
replica. Distinguish an already-used expired lease from a sample that expires before any selection.
Inspect actual application trace metadata, dropped/incomplete events and fixture/test outcomes.
Preserve the entire failed report and trace. A trace alone does not grant permission to renew input,
extend a lease or weaken a gate. Any repair requires a causal boundary and contrasting regression.

The prior build is reused only while actual runtime/test source and all tracked binary hashes match
its accepted build report. This does not inherit the earlier failed server result as a pass. Any
runtime or test edit invalidates this reuse and requires a new build and affected checks. Core rules,
canonical state, full scope, explicit actor selection and native/production gates remain unchanged.
