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
    }
}