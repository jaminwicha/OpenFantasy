"""Summarize bounded opt-in timing captures; durations are wall time, not CPU attribution."""
import collections,json,statistics,sys
from pathlib import Path
path=Path(sys.argv[1])
report=json.loads(path.read_text()); frequency=report['TimestampFrequency']
events=sorted(report['Events'],key=lambda e:(e['Timestamp'],e['Sequence']))
pending={}; spans=collections.defaultdict(list); unmatched=collections.Counter(); movement=collections.defaultdict(list)
for event in events:
 if event['Kind']!='stage':continue
 stage=event['Stage']; owner=event['OwnerId']
 if stage.endswith('.begin'):
  key=(owner,stage[:-6])
  if key in pending:unmatched[stage]+=1
  pending[key]=event
 elif stage.endswith('.end') or stage in ('client.startup.complete','client.startup.failed','observation.captured'):
  prefix='client.startup' if stage.startswith('client.startup.') else 'observation' if stage=='observation.captured' else stage[:-4]
  key=(owner,prefix); before=pending.pop(key,None)
  if before:
   spans[prefix].append({'milliseconds':(event['Timestamp']-before['Timestamp'])*1000/frequency,'owner':owner,'start':before['Timestamp'],'end':event['Timestamp'],'terminal':stage,'correlation':event.get('Correlation')})
  else:unmatched[stage]+=1
 if stage in ('movement.accepted','movement.rejected','movement.age','movement.selected','ready'):
  movement[owner].append(event)
summary={'capture':str(path),'writtenEvents':report['WrittenEvents'],'droppedEvents':report['DroppedEvents'],'incompleteEvents':report['IncompleteEvents'],'invalidEvents':report['InvalidEvents'],'durationSeconds':(report['EndedTimestamp']-report['StartedTimestamp'])/frequency,'stages':{},'unmatchedStages':dict(unmatched),'unfinished':[{'owner':key[0],'stage':key[1],'timestamp':value['Timestamp']} for key,value in pending.items()],'movement':{},'counters':[e for e in events if e['Kind']=='counters']}
for stage,values in spans.items():
 ordered=sorted(v['milliseconds'] for v in values)
 summary['stages'][stage]={'count':len(values),'totalMilliseconds':sum(ordered),'medianMilliseconds':statistics.median(ordered),'p95Milliseconds':ordered[min(len(ordered)-1,int(len(ordered)*.95))],'maximumMilliseconds':max(ordered),'largest':sorted(values,key=lambda v:v['milliseconds'],reverse=True)[:8]}
for owner,values in movement.items():
 ages=[v['Value']/10000 for v in values if v['Stage']=='movement.age']
 summary['movement'][owner]={'accepted':sum(v['Stage']=='movement.accepted' for v in values),'selected':sum(v['Stage']=='movement.selected' for v in values),'expiredSamples':sum(age>150 for age in ages),'maximumAgeMilliseconds':max(ages,default=0),'events':values}
out=path.with_suffix('.analysis.json');out.write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:v for k,v in summary.items() if k not in ('counters','movement','stages')},indent=2))
print(json.dumps({key:{k:v for k,v in value.items() if k!='largest'} for key,value in summary['stages'].items()},indent=2))
print('analysis:',out)
