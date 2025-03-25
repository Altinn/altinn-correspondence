namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class PatchDialogRequestMapper
    {
        internal static List<object> CreateRemoveGuiActionPatchRequest(int guiActionToRemoveIndex)
        {
            return new List<object>
            {
                new 
                {
                    op = "remove",
                    path = $"/guiActions/{guiActionToRemoveIndex}"
                }
            };
        }
    }
}