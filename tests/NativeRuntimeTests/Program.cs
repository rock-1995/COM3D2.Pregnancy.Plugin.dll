using COM3D2.Pregnancy.Plugin;

int checks = 0;
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
int Bakes(Maid m) => BellyMorphController.Bakes.Count(x => x.Maid == m);
Maid Character(float belly = .7f, float body = .4f)
{
    var result = new Maid();
    PregnancyAppearance.Saved[result] = new AppearanceValues(belly, body);
    GameMain.Instance.CharacterMgr.Maids.Add(result);
    return result;
}

var first = Character();
Hooks.BeginAll(first);
var body = Hooks.LoadPart(first, 0);
var cloth = Hooks.LoadPart(first, 6);
NativeAppearanceRuntime.MorphChanged(body.morph);
NativeAppearanceRuntime.MorphChanged(cloth.morph);
Hooks.Frame(first);
Check(Bakes(first) == 0, "bulk body/cloth load stays inside the native transaction");
Hooks.EndAll(first);
Check(Bakes(first) == 1 && BodyChanges.Completes.Last().Rebuilt, "one bulk build commits one bake after all native changes");
Check(PregnancyAppearance.Reads.Where(x => x.Maid == first).All(x => x.Value == new AppearanceValues(.7f, .4f)), "the independent appearance fields reach construction");

int reads = PregnancyAppearance.Reads.Count, prepared = BodyChanges.Prepares.Count;
for (int frame = 0; frame < 100; frame++) Hooks.Frame(first);
Check(Bakes(first) == 1 && PregnancyAppearance.Reads.Count == reads && BodyChanges.Prepares.Count == prepared,
    "idle frames perform no appearance reads, preparation or baking");
// A mask or native ResetBlendValues only changes visible triangles / reuses the
// cached vertices. It does not invoke the morph-change hook.
Hooks.Frame(first);
Check(Bakes(first) == 1, "visibility-only processing adds no geometry work");

Hooks.BeginAll(first);
Hooks.BeginAll(first); // A nested native caller must not end its parent batch.
Hooks.LoadPart(first, 7);
Hooks.EndAll(first);
Check(Bakes(first) == 1, "nested completion cannot submit the outer build early");
Hooks.EndAll(first);
Check(Bakes(first) == 2 && !BodyChanges.Completes.Last().Rebuilt, "clothing replacement commits once without a body reset");

NativeAppearanceRuntime.MorphChanged(body.morph);
NativeAppearanceRuntime.MorphChanged(cloth.morph);
NativeAppearanceRuntime.MorphChanged(body.morph);
Check(Bakes(first) == 2, "standalone native morph changes coalesce before the completion boundary");
BodyChanges.DuringComplete = m => NativeAppearanceRuntime.MorphChanged(body.morph);
Hooks.Frame(first);
BodyChanges.DuringComplete = null;
Check(Bakes(first) == 3, "native changes made while filling missing body inputs stay in the same commit");
Hooks.Frame(first);
Check(Bakes(first) == 3, "completion does not leave a second deferred bake");

var sequence = Character(.3f, .8f);
Hooks.StartSequence(sequence);
Hooks.Step(sequence, 0, () => Hooks.LoadPart(sequence, 0));
Hooks.Frame(sequence);
Hooks.Step(sequence, 0, () => Hooks.LoadPart(sequence, 6));
Hooks.Frame(sequence);
Hooks.Step(sequence, 1);
Check(Bakes(sequence) == 0, "sequential model phases cannot bake before customization and logical visibility");
Hooks.Step(sequence, 2, () => NativeAppearanceRuntime.MorphChanged(new TBodySkin(sequence, 0).morph));
Check(Bakes(sequence) == 0, "phase 2 native inputs wait for the final phase 3 slot visibility");
Hooks.Step(sequence, 3);
Check(Bakes(sequence) == 1, "sequential build commits once after phase 3 logical visibility");
Hooks.Step(sequence, 4);
Hooks.Step(sequence, 5);
Hooks.Frame(sequence);
Check(Bakes(sequence) == 1, "mask and finishing phases do not rebake the same build");

var other = Character(.9f, .1f);
Hooks.BeginAll(first);
Hooks.LoadPart(first, 6);
Hooks.BeginAll(other);
Hooks.LoadPart(other, 0);
Hooks.EndAll(other);
Check(Bakes(first) == 3 && Bakes(other) == 1, "interleaved characters have independent native transactions");
Hooks.EndAll(first);
Check(Bakes(first) == 4 && BellyMorphController.Bakes.Last(x => x.Maid == other).Belly == .9f, "a later character completion preserves the other character's own fields");

var deferred = Character();
deferred.body0.isLoadedBody = false;
Hooks.BeginAll(deferred);
Hooks.LoadPart(deferred, 0);
Hooks.EndAll(deferred);
Hooks.Frame(deferred);
Check(Bakes(deferred) == 0, "a body not yet loaded is not submitted");
deferred.body0.isLoadedBody = true;
Hooks.Frame(deferred);
Check(Bakes(deferred) == 1, "the deferred transaction submits once its body becomes available");

// During GameMain.Deserialize, CharacterMgr may build models before ExSaveData's
// post-deserialize callback swaps the slot. No Get may consume the old slot here.
PregnancyAppearance.Previews[first] = new AppearanceValues(.99f, .99f);
Hooks.BeginLoad();
Hooks.BeginLoad(); // Nested loads share the same gate.
reads = PregnancyAppearance.Reads.Count;
int bakesBeforeLoad = BellyMorphController.Bakes.Count;
Hooks.BeginAll(first);
Hooks.LoadPart(first, 0);
Hooks.EndAll(first);
Hooks.Frame(first);
Check(PregnancyAppearance.Reads.Count == reads && BellyMorphController.Bakes.Count == bakesBeforeLoad,
    "model creation during deserialization never reads old ExSave fields or bakes them");
Hooks.EndLoad();
Check(NativeAppearanceRuntime.LoadingSave && PregnancyAppearance.Reads.Count == reads,
    "the inner deserialization end cannot open the outer save gate");
PregnancyAppearance.Saved[first] = new AppearanceValues(.11f, .22f);
Hooks.EndLoad();
Hooks.Frame(first);
Check(!NativeAppearanceRuntime.LoadingSave && BellyMorphController.Bakes.Last(x => x.Maid == first).Belly == .11f,
    "after ExSaveData swaps slots, the outer end uses the new appearance");
Check(PregnancyAppearance.Reads.Skip(reads).Where(x => x.Maid == first).All(x => x.Value == new AppearanceValues(.11f, .22f)),
    "the previous preview cannot leak into the loaded slot");

// LoadBody_R calls UnInit during replacement, while an outer native sequence is
// still running. Retiring the old meshes must preserve that outer transaction.
var replacement = Character(.55f, .65f);
Hooks.StartSequence(replacement);
Hooks.Step(replacement, 0, () => { Hooks.Unload(replacement); Hooks.LoadPart(replacement, 0); });
Hooks.Frame(replacement);
Check(Bakes(replacement) == 0, "replacing a body within phase 0 preserves the sequence until its final mask");
Hooks.Step(replacement, 0, () => Hooks.LoadPart(replacement, 7));
Hooks.Step(replacement, 1);
Hooks.Step(replacement, 2);
Check(Bakes(replacement) == 0, "body replacement still waits for phase 3 after native sliders settle");
Hooks.Step(replacement, 3);
Check(Bakes(replacement) == 1 && BodyChanges.Completes.Last().Rebuilt, "body replacement performs one final bake after its native inputs");
Hooks.Step(replacement, 4); Hooks.Step(replacement, 5);

var retired = Character();
Hooks.StartSequence(retired);
Hooks.Step(retired, 0, () => Hooks.LoadPart(retired, 0));
// Production Patch_BodyChangeUnload routes Maid.Uninit to Forget. That forwarding
// is exercised by BodyRuntimeTests; this sequence verifies the runtime boundary.
NativeAppearanceRuntime.Forget(retired);
Hooks.Unload(retired);
reads = PregnancyAppearance.Reads.Count;
Hooks.Frame(retired);
Check(Bakes(retired) == 0 && PregnancyAppearance.Reads.Count == reads,
    "retiring a maid during a sequence clears the transaction rather than preserving an unfinished body replacement");
NativeAppearanceRuntime.RequestApply(retired);
Hooks.Frame(retired);
Check(Bakes(retired) == 1, "a retired maid can start a new transaction without stale sequential state");

int beforeRequests = Bakes(first);
for (int request = 0; request < 6; request++)
{
    PregnancyAppearance.Previews[first] = new AppearanceValues(request / 10f, request / 20f);
    NativeAppearanceRuntime.RequestApply(first);
}
Check(Bakes(first) == beforeRequests, "multiple maker requests do not bake immediately");
Hooks.Frame(first);
Check(Bakes(first) == beforeRequests + 1 && BellyMorphController.Bakes.Last(x => x.Maid == first).Belly == .5f,
    "maker requests coalesce once using the latest values");

Hooks.BeginLoad();
reads = PregnancyAppearance.Reads.Count;
Hooks.BeginAll(first);
Hooks.LoadPart(first, 0);
Hooks.EndAll(first);
int beforeFailure = BellyMorphController.Bakes.Count;
var failure = new IOException("broken save");
Check(ReferenceEquals(Hooks.FailLoad(failure), failure), "the save-load finalizer preserves the original game exception");
Check(!NativeAppearanceRuntime.LoadingSave && PregnancyAppearance.Reads.Count == reads,
    "a failed load releases the gate without reading a partly loaded slot");
Hooks.Frame(first);
Check(BellyMorphController.Bakes.Count == beforeFailure, "a failed load does not submit its incomplete model transactions");
NativeAppearanceRuntime.RequestApply(first);
Hooks.Frame(first);
Check(Bakes(first) == beforeRequests + 2, "a real subsequent request works after load failure");

Hooks.BeginLoad(); Hooks.BeginLoad();
Hooks.FailLoad(new IOException("inner save"));
Check(NativeAppearanceRuntime.LoadingSave, "an inner failure leaves the outer load gate closed");
Hooks.FailLoad(new IOException("outer save"));
Check(!NativeAppearanceRuntime.LoadingSave, "unwinding both failed loads releases the gate exactly once per call");

int saveCommits = PregnancyAppearance.SaveCommits;
Hooks.Call("Patch_NativeAppearance_Save", "Prefix");
Check(PregnancyAppearance.SaveCommits == saveCommits + 1, "save serialization commits independent appearance fields before serializing");

var brokenBuild = Character();
Hooks.BeginAll(brokenBuild); Hooks.BeginAll(brokenBuild);
Hooks.LoadPart(brokenBuild, 0);
int resets = BellyMorphController.Forgotten.Count;
var buildFailure = new InvalidOperationException("native model error");
Check(ReferenceEquals(Hooks.Call("Patch_NativeAppearance_AllProps", "Finalizer", brokenBuild, buildFailure), buildFailure),
    "synchronous native finalizer preserves the original failure");
Hooks.Call("Patch_NativeAppearance_AllProps", "Finalizer", brokenBuild, buildFailure);
reads = PregnancyAppearance.Reads.Count;
Hooks.Frame(brokenBuild);
Check(Bakes(brokenBuild) == 0 && PregnancyAppearance.Reads.Count == reads && BellyMorphController.Forgotten.Count == resets,
    "failed nested builds remove only bookkeeping, without resetting models or scheduling a frame retry");
Hooks.BeginAll(brokenBuild);
Hooks.LoadPart(brokenBuild, 0);
Hooks.EndAll(brokenBuild);
Check(Bakes(brokenBuild) == 1, "the next real native build is not blocked by a failed nested depth");

var brokenSequence = Character();
Hooks.StartSequence(brokenSequence);
Hooks.Step(brokenSequence, 0, () => Hooks.LoadPart(brokenSequence, 0));
Check(ReferenceEquals(Hooks.Call("Patch_NativeAppearance_SequenceStep", "Finalizer", brokenSequence, buildFailure), buildFailure),
    "sequential step finalizer preserves the original failure");
reads = PregnancyAppearance.Reads.Count;
Hooks.Frame(brokenSequence);
Check(Bakes(brokenSequence) == 0 && PregnancyAppearance.Reads.Count == reads,
    "a failed sequence does not leave a batch or idle retry behind");
NativeAppearanceRuntime.RequestApply(brokenSequence);
Hooks.Frame(brokenSequence);
Check(Bakes(brokenSequence) == 1, "a real request can prepare again after the sequence failed");
Hooks.StartSequence(brokenSequence);
Check(ReferenceEquals(Hooks.Call("Patch_NativeAppearance_SequenceStart", "Finalizer", brokenSequence, buildFailure), buildFailure),
    "failure while starting a sequence also releases its transaction");
NativeAppearanceRuntime.RequestApply(brokenSequence);
Hooks.Frame(brokenSequence);
Check(Bakes(brokenSequence) == 2, "start failure does not leave the next request blocked");

Console.WriteLine($"PASS: {checks} native transaction order checks.");
