using System.Linq;
using System.Windows.Forms;

namespace AgentDesk.Desktop;

public static class DisplayHelper
{
    public static Screen? GetSecondaryScreen()
    {
        return Screen.AllScreens.FirstOrDefault(s => !s.Primary);
    }
}
