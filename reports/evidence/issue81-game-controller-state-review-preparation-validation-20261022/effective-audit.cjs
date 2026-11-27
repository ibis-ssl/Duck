const fs=require('fs'),p=require('path'),cp=require('child_process'),crypto=require('crypto');
const h=x=>crypto.createHash('sha256').update(x).digest('hex');
const mods=['cspell','textlint','yaml'].map(name=>{const q=fs.realpathSync(p.join(process.cwd(),'node_modules',name,'package.json'));const b=fs.readFileSync(q);return {name,path:q,version:JSON.parse(b).version,sha256:h(b)};});
cp.spawnSync=(cmd,args,opts)=>{const c=JSON.parse(fs.readFileSync(args[args.indexOf('--config')+1]));const q=c.dictionaryDefinitions.find(x=>x.name==='markdown-whitelist').path;const b=fs.readFileSync(q); console.log(JSON.stringify({cwd:process.cwd(),cmd,args,config:c,dictionaryContent:b.toString(),dictionarySHA256:h(b),dictionaryTerms:b.toString().trim().split('\n').length,modules:mods}));return {status:0};};
process.argv=['node','run-cspell-markdown.js','Tracker/Design/tasks-status.md','Tracker/Design/phases-status.md'];
require('./tools/lint/scripts/run-cspell-markdown.js');