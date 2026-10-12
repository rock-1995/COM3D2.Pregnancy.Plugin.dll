namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        static void ApplyBodyChanges(Maid maid, float progress)
        {
            BodyChanges.State next;
            try { next = BodyChanges.Prepare(maid, progress); }
            catch (System.Exception e)
            {
                _log.LogWarning("Body change preparation: " + e.Message);
                return;
            }
            if (next == null) return;
            // Rebuild from the unmodified mesh when shape inputs change. This also
            // prevents the old belly deformation from becoming the new body baseline.
            ResetInternal(maid);
            bool suppress = _suppressMorphBaseBake;
            try
            {
                _suppressMorphBaseBake = true;
                BodyChanges.Apply(next);
            }
            catch (System.Exception e) { _log.LogWarning("Body change application: " + e.Message); }
            finally { _suppressMorphBaseBake = suppress; }
        }
    }
}
