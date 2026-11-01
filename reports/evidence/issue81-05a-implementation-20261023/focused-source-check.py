from pathlib import Path
import subprocess,hashlib,json
repo=Path("/home/donabe/Duck-issue81-aspire-desktop-bridge")
base="f71bb2688caf080382d170937ea932601e0b3c3f"
original=subprocess.run(["git","-C",str(repo),"show",f"{base}:Tracker/Tracker.Tests/AppHostApplicationModelTests.cs"],check=True,capture_output=True).stdout.decode()
current=(repo/"Tracker/Tracker.Tests/AppHostApplicationModelTests.cs").read_text()
def block(s,a,b): return s[s.index(a):s.index(b,s.index(a))]
markers=[("    public async Task BaseModeUsesPinnedRuntimeContainerFromItsLinuxPublishOutputWithoutPublishingUdp()","    [Fact]\n    public async Task ComparisonModeUsesPinnedDebugHostPublishOutputAndLoopbackDynamicTcp()"),("    public async Task ComparisonModeUsesPinnedDebugHostPublishOutputAndLoopbackDynamicTcp()","    [Fact]\n    public async Task ExplicitRefereeDriverSelectionUsesPinnedPublishOutputAndInternalGameControllerAddress()"),("    public async Task ExplicitRefereeDriverSelectionUsesPinnedPublishOutputAndInternalGameControllerAddress()","    [Fact]\n    public async Task ComparisonModeAddsPinnedExternalTrackersAndObserverWithoutAnotherRefereeProducer()"),("    private static void AssertLinuxDistribution(","    private static string FindRepositoryRoot()") ]
for a,b in markers:
 old,new=block(original,a,b),block(current,a,b)
 assert old==new,a
 print("fixed_block",a.split("(")[0].strip(),len(old.encode()),hashlib.sha256(old.encode()).hexdigest())
program=(repo/"Testing/Duck.Testing.AppHost/Program.cs").read_text()
for value in ["AddPublishedContainer(\"duck\", \"duck-runtime\", \"Tracker/Tracker.RuntimeHost\")","AddPublishedContainer(\"debug-host\", \"duck-debug-host\", \"Tracker/Tracker.DebugHost\")","AddPublishedContainer(\"referee-driver\", \"duck-referee-driver\", \"Testing/Duck.Testing.RefereeDriver.Host\")","WithHttpEndpoint(targetPort: 8080, name: \"http\", isProxied: false)","WithHttpEndpointCallback(endpoint => endpoint.TargetHost = \"127.0.0.1\", name: \"http\", isProxied: false)","WithHttpHealthCheck(\"/health/comparison\", endpointName: \"http\")","Testing__ContainerMode\", \"true\""]:
 assert value in program,value
assert "39107" not in program and "39109" not in program
for path,digest in [("Tracker/Tracker.RuntimeHost/Dockerfile","0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3"),("Tracker/Tracker.DebugHost/Dockerfile","8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca"),("Testing/Duck.Testing.RefereeDriver.Host/Dockerfile","8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca")]:
 data=(repo/path).read_text(); assert "sha256:"+digest in data and "COPY . /app" in data
 print("dockerfile",path,digest)
r=subprocess.run(["git","-C",str(repo),"diff","--check"],capture_output=True,text=True)
print("git_diff_check_exit",r.returncode)
print("git_diff_check_stdout",r.stdout)
print("git_diff_check_stderr",r.stderr)
assert r.returncode==0
for f in ["Testing/Duck.Testing.AppHost/Program.cs","Tracker/Tracker.Tests/AppHostApplicationModelTests.cs"]:
 print("current_file_sha256",f,hashlib.sha256((repo/f).read_bytes()).hexdigest())
