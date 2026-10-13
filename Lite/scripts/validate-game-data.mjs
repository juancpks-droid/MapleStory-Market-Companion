import {readFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
import {validGuide,validEntry} from '../lib/guide-contract.ts';
import {validReference} from '../lib/reference-contract.ts';
const root=resolve(process.argv[2]||'../SharedGameData');
const manifest=JSON.parse(await readFile(join(root,'manifest.json'),'utf8'));
const seen=new Set();let guides=0,references=0;
for(const entry of manifest.entries){
 if(!/^(guides|records)\/[a-z0-9-]+\.json$/.test(entry.file)||seen.has(entry.file))throw Error('Invalid or repeated record path');seen.add(entry.file);
 const bytes=await readFile(join(root,entry.file)),value=JSON.parse(bytes);
 if(bytes.length>262144||bytes.length!==entry.bytes||createHash('sha256').update(bytes).digest('hex')!==entry.sha256)throw Error('Record size or hash mismatch: '+entry.file);
 if(entry.file.startsWith('guides/')){if(!validGuide(value,value.path))throw Error('Invalid guide: '+entry.file);guides++;}
 else{if(!validReference(value,value.item?.key,value.kind))throw Error('Invalid reference: '+entry.file);references++;}
}
if(guides!==manifest.guideRecords||references!==manifest.referenceRecords)throw Error('Coverage count mismatch');
const bytes=await readFile(join(root,'guide-index.json'));const index=JSON.parse(bytes);
if(bytes.length>262144||index.schemaVersion!==1||!Array.isArray(index.entries)||index.entries.length>2000||!index.entries.every(validEntry))throw Error('Invalid guide directory');
for(const e of index.entries)if(!seen.has('guides/'+e.path.slice(1).replace('/','--')+'.json'))throw Error('Directory points to a missing guide');
console.log(`PASS: ${guides} guides, ${references} references, source/identity validation, directory targets, counts and SHA256 hashes.`);
