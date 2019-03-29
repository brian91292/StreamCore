using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore
{
    /// <summary>
    /// A collection of global variables meant to be used by any modules that make use of StreamCore
    /// </summary>
    public class Globals
    {
        /// <summary>
        /// The StreamCore data path.
        /// </summary>
        public static readonly string DataPath = Path.Combine(Environment.CurrentDirectory, "UserData", Plugin.ModuleName);

        /// <summary>
        /// This value will be true when the application is exiting.
        /// </summary>
        public static bool IsApplicationExiting = false;

        /// <summary>
        /// This value will be true when we're at the main menu.
        /// </summary>
        public static bool IsAtMainMenu = true;
    }
}
