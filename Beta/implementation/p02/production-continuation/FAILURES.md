# Corrections and retained failures

- **First solution build failed** (`qualification/build-01.log`): a new boundary test attempted to
  call the internal `WatershedRunoffCodecV1.EncodeRequest` from the separate test assembly. The test
  now requests the public request `ContentHash`, which exercises that canonical archive path and
  must refuse the continued owner. Runtime behavior and acceptance thresholds were not changed to
  satisfy the test. A subsequent build and test result are recorded separately.
- **Independent source review found a storage-lineage gap before tests ran.** Continued validation
  checked the aggregate custody revision and lot prefixes but did not retain per-storage revision
  baselines. An otherwise matching inventory could reset a storage revision and then prepare a
  request against that reset value. The fix retains bounded storage baselines and checks exact
  per-custodian operation increments, plus immutable non-custody admission pins. The new negative
  tests distinguish this invalid supplied proposal from the genuine accepted inventory. This is a
  substantive correction; it is not waived by the whole-world guard or by a prior build.

No full-world/Host/native continuation gate is claimed by the standalone component tests. Initial
reports remain retained; later success does not rewrite their outcome.
