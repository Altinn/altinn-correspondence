namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class DialogPatchRequestBuilder
    {
        private List<object> _PatchDialogRequest = new List<object>();

        public List<object> Build()
        {
            return _PatchDialogRequest;
        }

        internal DialogPatchRequestBuilder WithRemoveGuiActionOperation(int guiActionToRemoveIndex)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = $"/guiActions/{guiActionToRemoveIndex}"
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithRemoveApiActionOperation(int apiActionToRemoveIndex)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = $"/apiActions/{apiActionToRemoveIndex}"
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithReplaceStatusOperation(string newStatus)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "replace",
                    path = "/status",
                    value = newStatus
                }
            );
            return this;
        }

        internal DialogPatchRequestBuilder WithRemoveExpiresAtOperation()
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "remove",
                    path = "/expiresAt"
                }
            );
            return this;
        }
        
        internal DialogPatchRequestBuilder WithReplaceSummaryOperation(string newSummary)
        {
            _PatchDialogRequest.Add(
                new
                {
                    op = "replace",
                    path = "/content/summary",
                    value = newSummary
                }
            );
            return this;
        }
    }
}