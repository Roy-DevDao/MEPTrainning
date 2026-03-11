using Autodesk.Revit.DB;

namespace PipeSystemTransfer.Infrastructure.Helpers
{
    internal class SilentFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (var failure in failuresAccessor.GetFailureMessages())
            {
                var severity = failure.GetSeverity();

                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                    continue;
                }

                if (severity == FailureSeverity.Error)
                {
                    if (failure.HasResolutionOfType(FailureResolutionType.DetachElements))
                    {
                        failure.SetCurrentResolutionType(FailureResolutionType.DetachElements);
                        failuresAccessor.ResolveFailure(failure);
                        continue;
                    }
                    if (failure.HasResolutionOfType(FailureResolutionType.SkipElements))
                    {
                        failure.SetCurrentResolutionType(FailureResolutionType.SkipElements);
                        failuresAccessor.ResolveFailure(failure);
                        continue;
                    }
                    if (failure.HasResolutionOfType(FailureResolutionType.DeleteElements))
                    {
                        failure.SetCurrentResolutionType(FailureResolutionType.DeleteElements);
                        failuresAccessor.ResolveFailure(failure);
                    }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
