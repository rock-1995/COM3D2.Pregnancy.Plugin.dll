from pathlib import Path
root=Path(__file__).resolve().parent.parent
source=(root/'src/BellyMorphController.cs').read_text(encoding='utf-8-sig')
path=root/'tests/AdapterTests/LifecycleSlice.cs'
target=path.read_text(encoding='utf-8-sig')
def span(text, signature):
    start=text.index(signature); opening=text.index('{', start); depth=0
    for end in range(opening,len(text)):
        if text[end]=='{': depth+=1
        elif text[end]=='}':
            depth-=1
            if depth==0: return start,end+1
    raise ValueError(signature)
for sig in ('public static void ApplyProgress(Maid maid, float progress)', 'public static void Reset(Maid maid)', 'void ProcessVisibilityApply()'):
    a,b=span(source,sig); c,d=span(target,sig)
    target=target[:c]+source[a:b]+target[d:]
path.write_text(target,encoding='utf-8')
print('Synchronized changed production lifecycle methods into Unity test harness.')
