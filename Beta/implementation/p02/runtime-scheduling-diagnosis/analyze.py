"""Correlate bounded same-process runtime counters with original host timing events."""
import hashlib,json,sys
from pathlib import Path

out=Path(sys.argv[1]).resolve()
runtime_path=max(out.glob('runtime-scheduling-*.json'),key=lambda p:json.loads(p.read_text())['WrittenEvents'])
runtime=json.loads(runtime_path.read_text())
host_path=max(out.glob(f"host-timing-{runtime['ProcessId']}-*.json"),key=lambda p:json.loads(p.read_text())['WrittenEvents'])
host=json.loads(host_path.read_text())
for data in (host,runtime):
 for key in ('DroppedEvents','IncompleteEvents','InvalidEvents'):assert data[key]==0,(key,data[key])
assert runtime['SamplingFailures']==runtime['ListenerFailures']==0
assert host['ProcessId']==runtime['ProcessId'] and host['RuntimeVersion']==runtime['RuntimeVersion']=='10.0.12'
assert host['TimestampFrequency']==runtime['TimestampFrequency']
events=sorted(runtime['Events'],key=lambda e:e['EventTimestamp'])
freq=runtime['TimestampFrequency'];ticks=runtime['TimeSpanTicksPerSecond']
host_stages={(e['OwnerId'],e['Stage'],e['Timestamp']) for e in host['Events'] if e['Kind']=='stage'}
assert all((e['OwnerId'],e['Stage'],e['EventTimestamp']) in host_stages for e in events)
tick_owners={e['OwnerId'] for e in events if e['Stage']=='tick.wait'}
bindings={}
for e in host['Events']:
 if e['Kind']=='bind':bindings.setdefault(e['OwnerId'],set()).add(e['RelatedOwnerId'])

def span(a,b):
 assert b['SamplingStartedTimestamp']>=a['SamplingCompletedTimestamp'], 'Counter samples overlap; interval is not interpretable'
 assert b['ProcessCpuTimeTicks']>=a['ProcessCpuTimeTicks'] and b['GcPauseTimeTicks']>=a['GcPauseTimeTicks']
 assert all(b[f'Generation{i}Collections']>=a[f'Generation{i}Collections'] for i in range(3))
 return {'fromSequence':a['Sequence'],'toSequence':b['Sequence'],
  'wallMilliseconds':(b['EventTimestamp']-a['EventTimestamp'])*1000/freq,
  'counterSamplingStartToStartMilliseconds':(b['SamplingStartedTimestamp']-a['SamplingStartedTimestamp'])*1000/freq,
  'fromEventToSamplingMilliseconds':(a['SamplingStartedTimestamp']-a['EventTimestamp'])*1000/freq,
  'toEventToSamplingMilliseconds':(b['SamplingStartedTimestamp']-b['EventTimestamp'])*1000/freq,
  'processCpuMilliseconds':(b['ProcessCpuTimeTicks']-a['ProcessCpuTimeTicks'])*1000/ticks,
  'gcPauseMilliseconds':(b['GcPauseTimeTicks']-a['GcPauseTimeTicks'])*1000/ticks,
  'generationCollections':[b[f'Generation{i}Collections']-a[f'Generation{i}Collections'] for i in range(3)],
  'fromPendingWorkItems':a['PendingWorkItems'],'toPendingWorkItems':b['PendingWorkItems'],
  'fromThread':a['ManagedThreadId'],'toThread':b['ManagedThreadId']}

movements=[]
for i,a in enumerate(events):
 if a['Stage']!='movement.accepted' or a['Correlation']==a['Value']==0:continue
 tail=[];next_input=None
 for b in events[i+1:]:
  if b['OwnerId']!=a['OwnerId']:continue
  if b['Stage']=='movement.accepted':
   next_input=b
   break
  tail.append(b)
 ages=[b for b in tail if b['Stage']=='movement.age']
 if not ages:
  movements.append({'port':a['OwnerId'],'acceptanceSequence':a['Sequence'],'firstSampleAgeMilliseconds':None,
   'selectedCount':sum(b['Stage']=='movement.selected' for b in tail),'status':'unsampled-before-next-input' if next_input else 'unsampled-before-capture-end',
   'nextAcceptedInput':None if next_input is None else {'sequence':next_input['Sequence'],'x':next_input['Correlation'],'y':next_input['Value'],**span(a,next_input)}})
  continue
 first=ages[0];owner_hosts=bindings.get(a['OwnerId'],set())&tick_owners
 item={'port':a['OwnerId'],'acceptanceSequence':a['Sequence'],
  'firstSampleAgeMilliseconds':first['Value']/10000,'selectedCount':sum(b['Stage']=='movement.selected' for b in tail),
  'acceptanceToFirstSample':span(a,first)}
 if len(owner_hosts)==1:
  owner=owner_hosts.pop();item['host']=owner
  waits=[b for b in events if b['OwnerId']==owner and b['Stage']=='tick.wait' and b['EventTimestamp']<first['EventTimestamp']]
  if waits:
   wait=waits[-1]
   ends=[b for b in events if b['OwnerId']==owner and b['Stage']=='tick.end' and b['EventTimestamp']<wait['EventTimestamp']]
   if ends:item['previousTickEndToNextWait']=span(ends[-1],wait)
 movements.append(item)

intervals=[];opened={}
for e in events:
 for begin,end,label in [('observation.begin','observation.captured','observation'),('replica.begin','replica.end','replica')]:
  key=(e['OwnerId'],label)
  if e['Stage']==begin:opened[key]=e
  if e['Stage']==end and key in opened:
   a=opened.pop(key);intervals.append({'owner':e['OwnerId'],'component':label,**span(a,e)})
costs=sorted((e['SamplingCompletedTimestamp']-e['SamplingStartedTimestamp'])*1000/freq for e in events)
lags=sorted((e['SamplingStartedTimestamp']-e['EventTimestamp'])*1000/freq for e in events)
result={'applicationProcessId':runtime['ProcessId'],'runtimeVersion':runtime['RuntimeVersion'],
 'inputs':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (runtime_path,host_path)},
 'matchedRuntimeEvents':len(events),'samplingMilliseconds':{'sum':sum(costs),'median':costs[len(costs)//2],
  'p95':costs[int(len(costs)*.95)],'maximum':costs[-1]},
 'eventToSamplingMilliseconds':{'median':lags[len(lags)//2],'p95':lags[int(len(lags)*.95)],'maximum':lags[-1]},'movements':movements,
 'longestObservationIntervals':sorted(intervals,key=lambda i:i['wallMilliseconds'],reverse=True)[:20],
 'limits':['Process-wide counters are non-atomic and may include parallel unrelated test work.',
 'Sampling spans exclude dispatch/allocation/publication overhead; instrumentation may perturb execution.',
 'Expired samples may be intentional in other cases; correlate the original test result and host identity.',
 'This evidence alone does not qualify a scheduler repair or close the production timing gate.']}
(out/'ANALYSIS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('movements','longestObservationIntervals','limits')},indent=2))
print(json.dumps([m for m in movements if m['firstSampleAgeMilliseconds'] is None
 or m['firstSampleAgeMilliseconds']>150 and m['selectedCount']==0],indent=2))
