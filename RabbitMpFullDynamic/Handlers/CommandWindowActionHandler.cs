using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using System.Diagnostics;

namespace RabbitMpFullDynamic
{
    [ActionHandler("RabbitMpFullDynamic.CommandWindowAction")]
    public class CommandWindowActionHandler : IActionHandler
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nexUpdate)
        {
            // return true/false to enable/disable this action
            return true;
        }
        
        public void Execute(IDataContext context,DelegateExecute nextExecute)
        {
            Process.Start("cmd");
        }        
    }
}