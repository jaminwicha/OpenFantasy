#!/usr/bin/env python3
"""Validate source truth, complete field coverage, dependency closure and bounded target claims."""
from collections import Counter
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent
PACKET_PATH='Beta/Runtime/content/legacy/aldermere-first-loop'
PRIMARY={f'openfantasy.aldermere:{key}' for key in ['npcs/farmer_wren','quests/wren_orchard','items/wood','items/berries','items/rations','recipes/craft_rations']}
CONTEXT={'openfantasy.aldermere:nodes/101','openfantasy.aldermere:nodes/103'}
CODE={'openfantasy.aldermere:node_archetypes/oak','openfantasy.aldermere:node_archetypes/berry'}
DISPOSITIONS={'mapped_to_existing_contract','pending_explicit_transform_or_gameplay','retained_source_only'}
TARGET_DECLARATIONS={
    'package-namespace':('Content/PackageId.cs','public sealed record PackageId'),
    'content-hash':('Content/ContentHash.cs','public sealed record ContentHash'),
    'entity-id':('EntityId.cs','public readonly record struct EntityId'),
    'material-key':('Production/MaterialDefinition.cs','public string Key { get; }'),
    'recipe-key':('Production/MaterialRecipeDefinition.cs','public string Key { get; }'),
    'recipe-count':('Production/RecipeTerm.cs','public ulong Count { get; }'),
    'recipe-role':('Production/RecipeTerm.cs','public enum RecipeMaterialRole'),
    'material-pin':('Custody/CountedItemDefinition.cs','public sealed record CountedItemDefinition'),
    'body-definition':('Actors/BodyDefinition.cs','public sealed class BodyDefinition'),
    'form-binding':('Actors/ActorFormBinding.cs','public sealed record ActorFormBinding'),
    'calendar-coordinate':('Scheduling/CalendarClockCoordinate.cs','public sealed record CalendarClockCoordinate'),
    'world-address':('History/HistoryAddress.cs','public readonly record struct HistoryAddress'),
}
# These semantic witnesses are independently specified here, not imported from the builder.
EVIDENCE_DECLARATIONS={
    'constants':('src/Sim/SimConstants.cs',{'public const int TicksPerSecond = 20;','public const int UnitsPerTile = 32;'}),
    'npc-defaults':('src/Sim/GameData.cs',{'public List<string> Shop { get; set; } = new();','public string Prerequisite { get; set; } = "";','public string RewardItem { get; set; } = "";','public CraftStation Station { get; set; } = CraftStation.None;'}),
    'npc-spawn':('src/Sim/World.Systems.cs',{'X = def.TileX * u + u / 2,','Hp = 1,','MoveSpeed = 2,'}),
    'quest-fetch':('src/Sim/World.Systems.cs',{'QuestKind.Fetch => CountItem(player, quest.TargetItem) >= quest.TargetCount,','RemoveItem(player, quest.TargetItem, quest.TargetCount);','player.Gil += quest.RewardGil;','GrantQuestXp(player, quest.RewardXp);'}),
    'gathering':('src/Sim/World.Gathering.cs',{'private const int GatherAtbCost = 400;','private const int GatherRange = 56;','node.Hp--;','int amount = tool.ToolPower;'}),
    'crafting':('src/Sim/World.Gathering.cs',{'RemoveItem(player, input.Item, input.Count);','if (!AddItem(player, recipe.Output, recipe.OutputCount))'}),
    'consuming':('src/Sim/World.Systems.cs',{'int hp = Math.Min(item.HealHp, player.MaxHp - player.Hp);','int mp = Math.Min(item.HealMp, player.MaxMp - player.Mp);'}),
}

class MappingError(ValueError): pass


def digest(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()
def require(condition, message):
    if not condition: raise MappingError(message)


def pointer(value, address):
    for part in address.split('/')[1:]:
        key=part.replace('~1','/').replace('~0','~')
        value=value[int(key)] if isinstance(value,list) else value[key]
    return value


def enumerate_fields(value):
    found={}
    pending=[('',value)]
    while pending:
        address,current=pending.pop()
        children=current.items() if isinstance(current,dict) else enumerate(current) if isinstance(current,list) else []
        for key,child in children:
            child_address=address+'/'+str(key).replace('~','~0').replace('/','~1')
            require(child_address not in found,'Duplicate computed pointer')
            found[child_address]=child
            if isinstance(child,(dict,list)): pending.append((child_address,child))
    return found


def unique(rows,key,what):
    result={}
    for row in rows:
        require(row[key] not in result,f'Duplicate {what}: {row[key]}')
        result[row[key]]=row
    return result


def validate(document, root=ROOT):
    packet=root/PACKET_PATH
    require(document['schemaVersion']==1 and document['status']=='field_mapping_review_not_playable_migration','Unknown mapping schema/status')
    require(document['scope']['notFullMigration'] is True,'Must not claim full migration')
    for field,expected in [('primaryRecords',PRIMARY),('gatheringContextRecords',CONTEXT),('codeContextKeys',CODE)]:
        values=document['scope'][field]
        require(len(values)==len(set(values)) and set(values)==expected,'Selected scope changed: '+field)
    require(set(document['sourcePacket'])=={'records.json','references.json','closure-manifest.json','source-key-map.json'},'Missing/unknown source packet fingerprint')
    for name,expected in document['sourcePacket'].items():
        require(digest((packet/name).read_bytes())==expected,'Source packet drift: '+name)
    original=json.loads((packet/'records.json').read_text())
    original_rows=unique(original['records'],'targetKey','source record')
    original_code=unique(original['codeDefinedRecords'],'targetKey','source code record')
    manifest=json.loads((packet/'closure-manifest.json').read_text())
    original_edges=json.loads((packet/'references.json').read_text())['edges']
    source_files={entry['sourcePath']:entry for entry in manifest['sources']}
    source_support={entry['targetKey']:entry for entry in manifest['sources']}
    rows=unique(document['records'],'sourceKey','mapping record')
    require(set(rows)==PRIMARY|CONTEXT,'Missing or extra selected record')
    policy=document['targetPolicy']
    require(policy['namespace']=='openfantasy.aldermere' and policy['automaticRuntimeAdmission'] is False,'Unapproved target admission policy')
    targets=document['knownTargets']
    require(policy['namespaceContract']=='package-namespace' and policy['sourceHashContract']=='content-hash','Wrong namespace/hash contract')
    require(set(targets)==set(TARGET_DECLARATIONS),'Missing/unknown target contract')
    for name,target in targets.items():
        expected_path,expected_marker=TARGET_DECLARATIONS[name]
        require(target['path']=='Beta/Runtime/src/OpenFantasy.Core/'+expected_path and target['marker']==expected_marker,'Relabeled target declaration: '+name)
        path=Path(target['path'])
        require(not path.is_absolute() and '..' not in path.parts and str(path).startswith('Beta/Runtime/src/OpenFantasy.Core/'),'Invalid target path: '+name)
        data=(root/path).read_bytes()
        require(digest(data)==target['sha256'],'Target source drift: '+name)
        require(target['marker'] in data.decode() and target['meaning'].strip(),'Unknown target declaration: '+name)
    selected=PRIMARY|CONTEXT|CODE
    expected_edges=[edge for edge in original_edges if edge['from'] in selected]
    require(sorted(map(canonical,document['dependencyEdges']))==sorted(map(canonical,expected_edges)),'Selected field dependency edge lost/changed/invented')
    require(len(set(map(canonical,document['dependencyEdges'])))==len(document['dependencyEdges']),'Duplicate dependency edge')
    required_support={edge['to'] for edge in expected_edges}-selected
    support=unique(document['dependencySupport'],'targetKey','dependency support')
    require(set(support)==required_support,'Missing/extra dependency support endpoint')
    for key,entry in support.items():
        require(canonical(entry)==canonical(source_support[key]),'Changed source support metadata')
        data=(packet/entry['retainedPath']).read_bytes()
        require(len(data)==entry['bytes'] and digest(data)==entry['sha256'],'Retained source dependency bytes drifted')
    total_fields=0
    dispositions=Counter()
    for key,row in rows.items():
        original_row=original_rows[key]
        require(row['scope']==('primary' if key in PRIMARY else 'gathering_context'),'Invalid record scope')
        require(canonical(row['sourceIdentity'])==canonical(original_row['sourceIdentity']),'Changed original identity metadata')
        payload=original_row['sourcePayload']
        require(canonical(row['sourcePayload'])==canonical(payload),'Changed original payload')
        identity=row['sourceIdentity']
        require(digest(canonical(payload))==identity['payloadSha256'],'Original payload hash mismatch')
        retained_file=source_files[identity['path']]
        file_bytes=(packet/retained_file['retainedPath']).read_bytes()
        require(digest(file_bytes)==retained_file['sha256'],'Original retained data file drift')
        actual_payload=pointer(json.loads(file_bytes),identity['jsonPointer'])
        require(canonical(actual_payload)==canonical(payload),'Original JSON pointer does not resolve to payload')
        target=row['target']
        require(target['namespace']=='openfantasy.aldermere' and target['preservedKey']==key,'Source alias changed')
        require(target['definitionId'] is None and target['acceptedVersion'] is None and target['runtimeAdmission']=='not_admitted','Unapproved runtime identity/version or admission claim')
        require(bool(target['versionPolicy'].strip()),'Missing explicit version policy')
        expected_fields=enumerate_fields(payload)
        covered=unique(row['fields'],'relativePointer','field')
        require(set(covered)==set(expected_fields),'Incomplete/extra source field coverage: '+key)
        for address,source_value in expected_fields.items():
            field=covered[address]
            require(field['sourcePointer']==identity['jsonPointer']+address,'Wrong source field pointer')
            require(canonical(field['sourceValue'])==canonical(source_value),'Changed source field value')
            require(field['valueSha256']==digest(canonical(source_value)),'Source field hash mismatch')
            require(field['disposition'] in DISPOSITIONS,'Unknown field disposition')
            require(field['meaning'].strip() and field['units'].strip(),'Missing field semantics or units')
            require(len(field['targetReferences'])==len(set(field['targetReferences'])) and all(ref in targets for ref in field['targetReferences']),'Unknown/duplicate target reference')
            expected_links=[edge for edge in expected_edges if edge['from']==key and (edge['field']==address or address.startswith(edge['field']+'/'))]
            require(sorted(map(canonical,field['sourceDependencies']))==sorted(map(canonical,expected_links)),'Field dependency omission/change')
            conversion=field['faithfulFieldTransform']
            if field['disposition']=='mapped_to_existing_contract':
                family=original_row['family']
                if address=='/id' and family in ('items','recipes'):
                    expected_target='material-key' if family=='items' else 'recipe-key'
                    require(field['targetReferences']==[expected_target] and conversion=={'operation':'copy_key','value':source_value},'Unsupported key transform')
                elif family=='recipes' and (address=='/outputCount' or address in ('/inputs/0/count','/inputs/1/count')):
                    role='Output' if address=='/outputCount' else 'Input'
                    require(type(source_value) is int and 0<source_value<2**64,'Count cannot map losslessly to UInt64')
                    require(field['targetReferences']==['recipe-count','recipe-role'] and conversion=={'operation':'positive_count','role':role,'value':source_value},'Unsupported counted recipe transform')
                else: raise MappingError('Gameplay/tuning field falsely marked mapped: '+key+address)
            else:
                require(conversion is None,'Unapproved conversion on pending/retained field')
            total_fields+=1
            dispositions[field['disposition']]+=1
    code_rows=unique(document['codeContext'],'targetKey','code context')
    require(set(code_rows)==CODE,'Missing code-defined context')
    for key,row in code_rows.items():
        require(canonical(row)==canonical(original_code[key]),'Changed exact code-defined record')
        require(digest(row['sourceExactText'].encode())==row['sourceIdentity']['exactTextSha256'],'Code archetype exact-text hash mismatch')
        file_entry=source_files[row['sourceIdentity']['path']]
        code_bytes=(packet/file_entry['retainedPath']).read_bytes()
        require(digest(code_bytes)==row['sourceIdentity']['sourceSha256'] and row['sourceExactText'] in code_bytes.decode(),'Code-defined source witness mismatch')
    require(set(document['evidence'])==set(EVIDENCE_DECLARATIONS),'Missing/unknown semantic source witness')
    for name,evidence in document['evidence'].items():
        expected_path,expected_markers=EVIDENCE_DECLARATIONS[name]
        markers=evidence['requiredMarkers']
        require(evidence['originalPath']==expected_path and len(markers)==len(expected_markers) and set(markers)==expected_markers,'Relabeled semantic source witness: '+name)
        require(evidence['originalPath'] in source_files,'Unknown semantic source evidence')
        require(evidence['retainedPath']==source_files[evidence['originalPath']]['retainedPath'],'Wrong retained evidence path')
        data=(packet/evidence['retainedPath']).read_bytes()
        require(digest(data)==evidence['sha256']==source_files[evidence['originalPath']]['sha256'],'Semantic source evidence drift')
        require(evidence['requiredMarkers'] and all(marker in data.decode() for marker in evidence['requiredMarkers']),'Missing source semantic/default marker')
    expected_defaults={('openfantasy.aldermere:recipes/craft_rations','/station'):'None',('openfantasy.aldermere:npcs/farmer_wren','/shop'):[],('openfantasy.aldermere:quests/wren_orchard','/prerequisite'):'',('openfantasy.aldermere:quests/wren_orchard','/rewardItem'):''}
    seen=set()
    for default in document['faithfulMissingDefaults']:
        address=(default['sourceKey'],default['missingField'])
        require(address in expected_defaults and address not in seen,'Unexpected/duplicate missing-field default')
        seen.add(address)
        require(default['missingField'] not in enumerate_fields(rows[default['sourceKey']]['sourcePayload']),'Default overrides an actual source field')
        require(canonical(default['sourceDefault'])==canonical(expected_defaults[address]),'Invented default value')
        require(default['evidence']=='npc-defaults' and default['meaning'].strip(),'Default has no actual code witness')
    require(seen==set(expected_defaults),'Missing declared source defaults')
    counts={'primaryRecords':6,'gatheringContextRecords':2,'codeContextRecords':2,'fieldRows':total_fields,'dependencyEdges':len(expected_edges),'dispositions':dict(sorted(dispositions.items()))}
    require(document['counts']==counts,'Incorrect proof counts')
    return {'status':'verified_field_mapping_not_gameplay','counts':counts,'targetReferences':len(targets),'sourceEvidenceRecords':len(document['evidence']),'faithfulMissingDefaults':len(seen)}


if __name__=='__main__':
    print(json.dumps(validate(json.loads((HERE/'mapping-v1.json').read_text())),indent=2))
