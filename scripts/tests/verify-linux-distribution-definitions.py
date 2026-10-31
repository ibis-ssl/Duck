#!/usr/bin/env python3
"""固定参照と実際のLinux発行出力の配布定義を照合する。"""
from __future__ import annotations
import argparse, hashlib, json, os, pathlib, re, sys

class ContractError(Exception):
    """配布定義または実発行出力が固定契約を満たさない場合の診断。"""

RUNTIME = "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3"
ASPNET = "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca"
APPS = {
 "RuntimeHost": {"dockerfile":"Tracker/Tracker.RuntimeHost/Dockerfile","output":"Tracker.RuntimeHost","assembly":"Tracker.RuntimeHost.dll","framework":"Microsoft.NETCore.App","base":RUNTIME,"static":["appsettings.json"]},
 "DebugHost": {"dockerfile":"Tracker/Tracker.DebugHost/Dockerfile","output":"Tracker.DebugHost","assembly":"Tracker.DebugHost.dll","framework":"Microsoft.AspNetCore.App","base":ASPNET,"static":["appsettings.json","wwwroot/app.css","wwwroot/favicon.png"]},
 "RefereeDriver.Host": {"dockerfile":"Testing/Duck.Testing.RefereeDriver.Host/Dockerfile","output":"Duck.Testing.RefereeDriver.Host","assembly":"Duck.Testing.RefereeDriver.Host.dll","framework":"Microsoft.AspNetCore.App","base":ASPNET,"static":[]},
}
# tag/index root values and descriptors are sourced from the preserved 04B2 metadata bundle.
LOCK = {
"dotnet-runtime":("mcr.microsoft.com/dotnet/runtime:10.0","sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3","application/vnd.docker.distribution.manifest.list.v2+json",[("linux","amd64","sha256:1bac183f0f5e1b7dd58cf1083fa79e6310d47afe3ca5c9130c910ebba7720929"),("linux","arm/v7","sha256:0e7e2b03b5f3c3d0546c7d6141f1b75f682b0bac02d11a508a438991ee322b2d"),("linux","arm64","sha256:9aaf1ab716a4e15a7c8cf773bcf7638ae1b5114bccee5611ce371e1a043d2033")],[]),
"dotnet-aspnet":("mcr.microsoft.com/dotnet/aspnet:10.0","sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca","application/vnd.docker.distribution.manifest.list.v2+json",[("linux","amd64","sha256:f879b8a9cb3f6694b8b0c9fa530eb0c39d8920db09c05a7edbd6c4c6c104ae1b"),("linux","arm/v7","sha256:e8be2c890668fb4b1d56484b10694c54e060fe362363d916f9847f374158a546"),("linux","arm64","sha256:3a05e37c371f358d2b5cb9687d9992171b34b8c9146c283bef865bdbd2c53c55")],[]),
"game-controller":("robocupssl/ssl-game-controller:3.20.3","sha256:d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7","application/vnd.oci.image.index.v1+json",[("linux","amd64","sha256:bd0d621373098bbf6f27243a2c1b9a219d6402a96dd30b9825ded5258c106721"),("linux","arm64","sha256:1a94486e2c5d6ac9ce5c443eeafe35bcd4af674598b41604ee6ab558d748ad72")],[("unknown","unknown","sha256:e497325a1c2e6ef46d93589d9df68caa2e30bd73656f27b6803fc6b6be326816"),("unknown","unknown","sha256:243c80d59bc75980db5e00213c52be63568ebf75994038a0a3545ae3abd96849")]),
"sumatra":("tigersmannheim/sumatra:2025","sha256:9ad3f65410f1c33b5b00400390dd3fb85c35ba6d76500fa68b4d51b4c3cd90c1","application/vnd.docker.distribution.manifest.list.v2+json",[("linux","amd64","sha256:0c05451f26aeb21bffde4c3d6858b6f441bd6bfaf8286f0ecd799241cb1fbc6c"),("linux","arm64","sha256:e07efb360effb09dc716f37943f46f0ae9eb3569f0c5f9bf4c6be88599c0b5d0")],[]),
"autoref":("roboticserlangen/autoref:2025.1.0","sha256:de4f06a9bcfad524b3bae2015f92bb3fc3a209295eb668a34d5a0b329616bf68","application/vnd.oci.image.index.v1+json",[("linux","amd64","sha256:04067106660f5ff31d1ebe4db9b19b1078a2124db3adabae79f6c9d23fa99317"),("linux","arm64","sha256:3453c128d3242e5a6eabfae53ed5c9a6b9c5a384a8aa9e69e2e4e5d932420373")],[("unknown","unknown","sha256:223a37d53f2eaeac74c2d4d044175e9a496854f2936042d5b98495f8e69f0100"),("unknown","unknown","sha256:216845b29c6318850983b182fa12b25f53fba383f7452e0833dc4631f7b6a2b0")]),
"framework-simulator":("ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd","sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79","application/vnd.oci.image.index.v1+json",[("linux","amd64","sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554")],[("unknown","unknown","sha256:949c77e64a8fd15b3e2de0d7a1e7221821ef0dcbdf36fb424c2458ecba927d3d")]),
"crane":("ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d","sha256:db6ab99009f98003c2f078e5d0820004a8e726c7a293965e7022ecfdb1bb2a49","application/vnd.oci.image.index.v1+json",[("linux","amd64","sha256:ce12b180bc689c792178b9c0990038f5f3acab9fd461f914356167cd170d47ac")],[("unknown","unknown","sha256:9999aa07fd81940f43946b81216742415a23ed04abfe1f37b328c620f8c4a697")]),
"orion-cm4-sim":("ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a","sha256:aef193194cce7c1c3729ac344065945b3d92c0a7ccdca0d5036cc472a8bfa61a","application/vnd.oci.image.index.v1+json",[("linux","amd64","sha256:e67b773e8b1391d755ec93d4dce30af074481fe02c1cdca21754aa1273c6223b")],[("unknown","unknown","sha256:f07d687d6ce8bd2d6cbc07bbe0acf32553fa6aef5a73f9221f357c8d3622b7ba")]),
}
APPHOST_PROGRAM_FRAGMENTS = [
    # 比較専用の参照と既定値はProgram.csに残る。
    "tigersmannheim/sumatra:{tigersImageTag}",
    "roboticserlangen/autoref:{erForceImageTag}",
    'builder.Configuration["Testing:Comparison:TigersImageTag"] ?? "2025"',
    'builder.Configuration["Testing:Comparison:ErForceImageTag"] ?? "2025.1.0"',
    # 通常構成の既定値は実際の登録処理へ渡される。
    'builder.Configuration["Testing:Simulator:ImageTag"] ?? "a52b6bd"',
    'builder.Configuration["Testing:Crane:ImageTag"] ?? "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d"',
    'builder.Configuration["Testing:Cm4Sim:ImageTag"] ?? "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a"',
]
APPHOST_REGISTRATION_CALL = (
    "BaseSimulationResourceRegistration.Add(builder,newBaseSimulationRegistrationOptions("
    "simulatorIbisPort,simulatorImageTag,simulatorGeometry,simulatorRealism,"
    "simulatorIbisTeamColor,craneImageTag,cm4SimulatorImageTag,"
)
APPHOST_REGISTRATION_OPTION_ORDER = (
    "stringSimulatorImageTag,stringSimulatorGeometry,stringSimulatorRealism,"
    "stringSimulatorIbisTeamColor,stringCraneImageTag,stringCm4SimulatorImageTag,"
)
APPHOST_REGISTRATION_FRAGMENTS = [
    "internal sealed record BaseSimulationRegistrationOptions(",
    "string SimulatorImageTag,",
    "string CraneImageTag,",
    "string Cm4SimulatorImageTag,",
    'ghcr.io/ibis-ssl/framework-simulatorcli:{options.SimulatorImageTag}',
    "robocupssl/ssl-game-controller:3.20.3",
    'ghcr.io/ibis-ssl/crane:{options.CraneImageTag}',
    'ghcr.io/ibis-ssl/orion-cm4-sim:{options.Cm4SimulatorImageTag}',
]

def parse_dockerfile(text: str) -> dict[str, list[list[str]]]:
    """固定範囲のDockerfile命令を解析する。"""
    result = {}
    for number, raw in enumerate(text.splitlines(),1):
        line=raw.strip()
        if not line or line.startswith("#"): continue
        match=re.match(r"^(FROM|WORKDIR|COPY|ENTRYPOINT)\s+(.+)$",line,re.I)
        if not match: raise ContractError(f"Dockerfile {number}行目に未対応命令があります: {line}")
        name,value=match.group(1).upper(),match.group(2).strip()
        if name=="ENTRYPOINT":
            try: args=json.loads(value)
            except json.JSONDecodeError as e: raise ContractError(f"Dockerfile {number}行目のENTRYPOINTがJSON配列ではありません") from e
            if not isinstance(args,list) or not all(isinstance(x,str) for x in args): raise ContractError("ENTRYPOINTは文字列配列でなければなりません")
        else: args=value.split()
        result.setdefault(name,[]).append(args)
    return result

def _check_lock(root: pathlib.Path, errors: list[str]) -> None:
    """固定8参照のtag、digest、媒体種別、platform descriptorを照合する。"""
    path=root/"Testing/Duck.Testing.AppHost/linux-image-lock.json"
    if not path.is_file(): errors.append(f"固定参照表がありません: {path}"); return
    try: data=json.loads(path.read_text(encoding="utf-8"))
    except (OSError,json.JSONDecodeError) as e: errors.append(f"固定参照表を読めません: {e}"); return
    images=data.get("images") if data.get("schemaVersion")==1 else None
    if not isinstance(images,dict): errors.append("固定参照表schemaVersion/imagesが不正です"); return
    if set(images)!=set(LOCK): errors.append(f"固定参照表のimage key集合が不一致です: {sorted(images)}")
    commits={"framework-simulator":"a52b6bdecd97a277544dd6f275afbf4483781e12","crane":"4063cd31cd5b11b1cc919003907f5f4c527b252d","orion-cm4-sim":"d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a"}
    for key,(ref,digest,media,platforms,others) in LOCK.items():
        item=images.get(key)
        if not isinstance(item,dict): errors.append(f"固定参照がありません: {key}"); continue
        for field,want in (("reference",ref),("digest",digest),("mediaType",media)):
            if item.get(field)!=want: errors.append(f"{key}.{field}が固定値と一致しません")
        for field,want in (("platforms",platforms),("otherDescriptors",others)):
            rows=item.get(field)
            got=sorted((r.get("os"),r.get("architecture"),r.get("digest")) for r in rows if isinstance(r,dict)) if isinstance(rows,list) else None
            if got!=sorted(want): errors.append(f"{key}.{field}が保存metadataと一致しません")
        if key in commits and item.get("sourceCommit")!=commits[key]: errors.append(f"{key}.sourceCommitが公開workflow記録と一致しません")

def _check_app(root: pathlib.Path, publish: pathlib.Path, name: str, app: dict, errors: list[str]) -> None:
    """Dockerfileと同じCI実行で生成した実発行ディレクトリを照合する。"""
    path=root/app["dockerfile"]
    if not path.is_file(): errors.append(f"Dockerfileがありません: {path}")
    else:
        try: instructions=parse_dockerfile(path.read_text(encoding="utf-8"))
        except (OSError,ContractError) as e: errors.append(f"Dockerfileを解析できません: {path}: {e}"); instructions={}
        expected={"FROM":[[app["base"]]],"WORKDIR":[["/app"]],"COPY":[[".","/app"]],"ENTRYPOINT":[["dotnet",app["assembly"]]]}
        for command,want in expected.items():
            if instructions.get(command)!=want: errors.append(f"{name}の{command}が実publish契約と一致しません")
        if set(instructions)-set(expected): errors.append(f"{name}に固定契約外のDockerfile命令があります")
    output=publish/app["output"]
    paths=[app["assembly"],app["assembly"].replace(".dll",".deps.json"),app["assembly"].replace(".dll",".runtimeconfig.json")]+app["static"]
    for relative in paths:
        if not (output/relative).is_file(): errors.append(f"{name}の実発行出力にDocker context用ファイルがありません: {relative}")
    runtime=output/app["assembly"].replace(".dll",".runtimeconfig.json")
    if runtime.is_file():
        try:
            options=json.loads(runtime.read_text(encoding="utf-8")).get("runtimeOptions",{})
            frameworks=options.get("frameworks",[])
            if not frameworks and "framework" in options: frameworks=[options["framework"]]
            if not any(x.get("name")==app["framework"] and x.get("version","").startswith("10.0.") for x in frameworks): errors.append(f"{name}のruntimeconfigが{app['framework']} 10.0.xを要求しません")
            if "includedFrameworks" in options: errors.append(f"{name}はframework-dependent publishでなければなりません")
        except (OSError,json.JSONDecodeError) as e: errors.append(f"{name}のruntimeconfigを解析できません: {e}")

def _check_apphost_definitions(repo_root: pathlib.Path, errors: list[str]) -> None:
    """Program.csから実登録処理まで固定image既定値の接続を検査する。"""
    program_path = repo_root / "Testing/Duck.Testing.AppHost/Program.cs"
    registration_path = repo_root / "Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs"
    try:
        program = program_path.read_text(encoding="utf-8")
    except OSError as e:
        errors.append(f"AppHost Program.csを読めません: {e}")
        return
    try:
        registration = registration_path.read_text(encoding="utf-8")
    except OSError as e:
        errors.append(f"共通登録処理を読めません: {e}")
        return

    compact_program = re.sub(r"\s+", "", program)
    compact_registration = re.sub(r"\s+", "", registration)
    for fragment in APPHOST_PROGRAM_FRAGMENTS:
        if fragment not in program:
            errors.append(f"Program.csの固定AppHost参照がありません: {fragment}")
    if APPHOST_REGISTRATION_CALL not in compact_program:
        errors.append("Program.csから共通登録処理へのimage tag引数接続が一致しません")
    if APPHOST_REGISTRATION_OPTION_ORDER not in compact_registration:
        errors.append("共通登録処理のimage tag option順がProgram.csと一致しません")
    for fragment in APPHOST_REGISTRATION_FRAGMENTS:
        if fragment not in registration:
            errors.append(f"共通登録処理の固定image参照がありません: {fragment}")


def verify_contract(repo_root: pathlib.Path, publish_root: pathlib.Path) -> None:
    """固定供給表、AppHostの既定参照、3つの実発行物を検証する。"""
    errors=[]
    _check_lock(repo_root,errors)
    _check_apphost_definitions(repo_root, errors)
    for name,app in APPS.items(): _check_app(repo_root,publish_root,name,app,errors)
    if errors: raise ContractError("\n".join("- "+e for e in errors))

def _check_cm4_helper_publish_output(publish_root: pathlib.Path, native_helper: pathlib.Path, errors: list[str]) -> None:
    """AppHost公開物に既存Linuxビルドと同じ補助実行物があることを確認する。"""
    published_helper = publish_root / "Duck.Testing.AppHost" / "cm4-transport-control"
    if not native_helper.is_file():
        errors.append(f"CM4 transport helper build output is missing: {native_helper}")
        return
    if not published_helper.is_file():
        errors.append(f"CM4 transport helper is missing from the AppHost publish output: {published_helper}")
        return
    native_hash = hashlib.sha256(native_helper.read_bytes()).hexdigest()
    published_hash = hashlib.sha256(published_helper.read_bytes()).hexdigest()
    if native_hash != published_hash:
        errors.append("CM4 transport helper in AppHost publish output differs from the statically verified Linux build.")
    if os.name == "posix" and not os.access(published_helper, os.X_OK):
        errors.append("CM4 transport helper in AppHost publish output is not executable.")

def main() -> int:
    """CIが作ったpublish rootを検証し違反を終了コード1で報告する。"""
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root",type=pathlib.Path,required=True)
    parser.add_argument("--publish-root",type=pathlib.Path,required=True)
    parser.add_argument("--native-helper",type=pathlib.Path,required=True)
    args=parser.parse_args()
    try:
        verify_contract(args.repo_root,args.publish_root)
        errors=[]
        _check_cm4_helper_publish_output(args.publish_root,args.native_helper,errors)
        if errors: raise ContractError("\n".join("- "+error for error in errors))
    except ContractError as e: print(str(e),file=sys.stderr); return 1
    print("固定参照、AppHost宣言、3つの管理対象出力、CM4補助実行物の同一性を確認しました。")
    return 0
if __name__=="__main__": raise SystemExit(main())
