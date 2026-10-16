#if DEBUG
using System.Collections.Generic;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.DesignData
{
    /// <summary>Manager sample with its rows. KC-12 replaces <see cref="Keycuts"/> with the real row view models.</summary>
    public class DesignManagerViewModel : ManagerViewModel
    {
        public DesignManagerViewModel(IReadOnlyList<Keycut> keycuts)
            : base(new RelayCommand(() => { }), new RelayCommand(() => { }))
        {
            Keycuts = keycuts;
            KeycutCount = keycuts.Count;
        }

        public IReadOnlyList<Keycut> Keycuts { get; }
    }

    /// <summary>
    /// Design-time data contexts, used from XAML as <c>d:DataContext="{x:Static design:DesignViewModels.ManagerTwoItems}"</c>.
    /// </summary>
    public static class DesignViewModels
    {
        public static CreatorViewModel CreatorEmpty
        {
            get { return NewCreator(string.Empty, string.Empty, 0); }
        }

        public static CreatorViewModel CreatorUrl
        {
            get { return NewCreator(SampleKeycuts.VickiDestination, SampleKeycuts.VickiName, 2); }
        }

        public static CreatorViewModel CreatorFolder
        {
            get { return NewCreator(SampleKeycuts.DownloadsDestination, SampleKeycuts.DownloadsName, 2); }
        }

        public static DesignManagerViewModel ManagerEmpty
        {
            get { return new DesignManagerViewModel(SampleKeycuts.Generate(0)); }
        }

        public static DesignManagerViewModel ManagerTwoItems
        {
            get { return new DesignManagerViewModel(SampleKeycuts.Canonical()); }
        }

        public static DesignManagerViewModel ManagerLongList
        {
            get { return new DesignManagerViewModel(SampleKeycuts.Generate(25)); }
        }

        private static CreatorViewModel NewCreator(string destination, string name, int count)
        {
            return new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }))
            {
                Destination = destination,
                ShortcutName = name,
                KeycutCount = count
            };
        }
    }
}
#endif
