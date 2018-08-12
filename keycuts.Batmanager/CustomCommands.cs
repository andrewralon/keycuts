using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace keycuts.Batmanager
{
    public static class CustomCommands
    {
        public static RoutedCommand Run = new RoutedCommand();

        public static RoutedCommand Edit = new RoutedCommand();

        public static RoutedCommand OpenDestinationLocation = new RoutedCommand();

        public static RoutedCommand Copy = new RoutedCommand();

        public static RoutedCommand Delete = new RoutedCommand();
    }
}
