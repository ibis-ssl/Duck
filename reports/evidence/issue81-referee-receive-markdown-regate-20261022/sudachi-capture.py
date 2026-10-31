import sys,importlib.util,json,dataclasses
from pathlib import Path
target,out,*args=sys.argv[1:]
spec=importlib.util.spec_from_file_location("sudachi_repo",target)
mod=importlib.util.module_from_spec(spec);sys.modules[spec.name]=mod;spec.loader.exec_module(mod)
seen=[];original=mod.record_violation
def record(source,line,token,normalized,kind,violations,unknown_words):
 original(source,line,token,normalized,kind,violations,unknown_words)
 seen.append(dataclasses.asdict(violations[-1]))
inputs=[];read=mod.read_inputs
def read_inputs(*args,**kwargs):
 result=list(read(*args,**kwargs));inputs.extend([p for p,_ in result]);return result
mod.record_violation=record;mod.read_inputs=read_inputs
sys.argv=[target]+args
code=mod.main()
Path(out).write_text(json.dumps({"exit_code":code,"inputs":inputs,"violations":seen},ensure_ascii=False,indent=2))
raise SystemExit(code)
