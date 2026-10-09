# Changes

## clothing-preview-20261010

This is the current local implementation snapshot, replacing the older source on the default branch. The final clothing change has no new settings and preserves the preceding local build's body deformation, body breast/arm exclusions, disabled upper-bone filter, outer clothing, and skirt solver.

- Inner clothing uses cached rest-body face correspondences and follows the three body vertices after their actual native/virtual skinning. Averaging the vertices' weights before skinning does not produce the same surface, which caused upper-abdomen clipping during torso motion despite a correct rest mesh.
- Inner-clothing breast guarding uses the body's breast-physics region. Completely breast-owned faces retain native chest motion; mixed chest/abdomen faces carry the fabric with the body. Garment breast weights alone do not freeze abdominal fabric.
- Shared body vertices and faces are evaluated once per pose. Unchanged poses reuse their cached transforms; the four-influence palette retains exact source/surface blending. Native skirt-weighted vertices bypass this new inner-clothing motion path.
- The accumulated source snapshot includes skirt-chain release/drape, mixed-weight transition handling, safe caching of optional bone slots, upper clothing contact fixes, and breast/arm exclusions from the preceding local revisions.

### Validation

- Release build for .NET 3.5: no warnings or errors.
- 4,274 self-contained adapter assertions and 26 breast-boundary assertions passed.
- 708,755 sparse-palette/skirt assertions passed across five models, including unused missing bones and restoration when a used bone is missing.
- Two bra models, seven poses each: support positions match actual body skinning within 0.00023 mm, and support-face corners within 0.0008 mm. Four-influence blending, bounds and caching checks passed.
- Large-bust body geometry, normals, attachment values and three poses match the earlier body baseline exactly, both clothed and unclothed at two stages.
- Five outer/skirt meshes retain exactly the preceding local version's static vertices and skirt-bone data.

### Known limitation

For the 1,003-vertex bra replay at stage 0.812, a −14.3° torso rotation reduced the worst upper-abdomen front-depth intersection from about 1.93 mm to 0.41 mm. Two short garment edges still intersect in their interiors even though their endpoints follow the body; differing body/garment triangulation remains relevant. This is not a guarantee of zero clipping for every garment or pose. No generic outward offset or garment retessellation was added.

Validation used actual plugin math with Unity stubs and local models; it is not an in-game visual or frame-rate test. Local model assets and dumps are not included in this repository.
