# Changes

## v13-20261010

### Changes since the clothing preview

- Preserve the original sag setting as **Skin slide / density** and add **Belly sag**, a downward displacement that preserves the navel's orientation.
- Use the latest tuned defaults. Navel protrusion progresses gradually from `NavelStart` (default `0.4`) to full protrusion at pregnancy progress `1`; full-navel preview is off by default. Saved user settings are retained.
- Reuse unchanged body calibration and mesh bindings during Apply and clothing visibility changes. Changed geometry, topology, settings or binding state invalidates the cache. Each maid retains her own applied settings; selecting or applying another maid no longer resets an existing deformation.
- Keep the exact `_SM_dress652_onep` model outside the skirt-bone solver. Other skirt rigs retain their normal solver.
- Exclude clothing vertices with positive lower-leg/knee/foot/toe bone influence and no skirt-bone ownership from both geometry deformation and abdominal virtual skinning. This includes minority calf weights and bone descendants; eligibility is independent of the current pose. Inner and outer clothing both retain native leg animation at these vertices.
- Navel accessories consume the body's actual final support vertices, bone weights and skinning matrices. Rebinding the body refreshes the support data, and accessory-first update order synchronizes the source palette. The existing rigid attachment shape and hidden-body fallback are retained.
- Include the **背中 / accSenaka** category in outer-clothing deformation and visibility refreshes.

### Validation

- .NET 3.5 production build: zero warnings and errors.
- 165,474 focused lower-leg/navel-binding/back-category assertions; 31,531 refresh and multi-maid assertions; 32,662 navel accessory assertions; 227,054 ordinary-skirt assertions; 111,603 exact skirt-exception assertions; 4,274 general adapter assertions passed.
- 391,293 default/navel-timing assertions and 2,844,268 sag assertions passed.
- In the 5,502-vertex dress652 replay, 2,604 lower-leg-influenced vertices retain their original positions and weights at progress 0.4, 0.815 and 1. Their posed positions match native leg animation exactly, including the three reported lifting vertices.
- The focused navel support test matches final body skinning within 0.0000002 model units, including replaced source weights/vertices and reversed update order.
- The seven-pose bra replay matches the preceding local V12 output exactly. The body-shape/default formulas and V12 refresh, ordinary skirt and inner-clothing motion implementations are unchanged by the final three fixes.

Tests exercise production algorithms and extracted lifecycle methods with Unity/game stubs and locally owned meshes. The back category uses a synthetic garment fixture. This exact build has not had in-game visual or timing validation. Model assets, personal dumps and local backups are not published. The earlier preview's garment edge-interior clipping limitation below is not claimed fixed.

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
