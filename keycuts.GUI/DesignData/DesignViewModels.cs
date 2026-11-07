#if DEBUG
using System.Collections.Generic;
using keycuts.Common;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI.DesignData
{
    /// <summary>Manager sample with its rows loaded; <see cref="Keycuts"/> keeps the raw sample data.</summary>
    public class DesignManagerViewModel : ManagerViewModel
    {
        public DesignManagerViewModel(IReadOnlyList<Keycut> keycuts)
            : base(new RelayCommand(() => { }), new RelayCommand(() => { }))
        {
            Keycuts = keycuts;
            ShowKeycuts(keycuts);
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
            get { return NewCreator(SampleKeycuts.VickiDestination, SampleKeycuts.VickiName, 2, KeycutKind.Url); }
        }

        public static CreatorViewModel CreatorFolder
        {
            get { return NewCreator(SampleKeycuts.DownloadsDestination, SampleKeycuts.DownloadsName, 2, KeycutKind.Folder); }
        }

        /// <summary>Destination chosen, name not typed yet (Create disabled).</summary>
        public static CreatorViewModel CreatorSelected
        {
            get { return NewCreator(SampleKeycuts.VickiDestination, string.Empty, 2, KeycutKind.Url); }
        }

        public static CreatorViewModel CreatorDragHover
        {
            get
            {
                var creator = NewCreator(string.Empty, string.Empty, 2);
                creator.IsDragHover = true;
                return creator;
            }
        }

        public static CreatorViewModel CreatorSaving
        {
            get
            {
                var creator = NewCreator(SampleKeycuts.VickiDestination, SampleKeycuts.VickiName, 2, KeycutKind.Url);
                creator.IsSaving = true;
                return creator;
            }
        }

        public static CreatorViewModel CreatorDuplicateName
        {
            get
            {
                var creator = NewCreator(SampleKeycuts.VickiDestination, SampleKeycuts.VickiName, 2, KeycutKind.Url);
                creator.SetDuplicateName(SampleKeycuts.VickiName);
                return creator;
            }
        }

        public static CreatorViewModel CreatorInvalidDestination
        {
            get
            {
                var creator = NewCreator(@"C:\does\not\exist\report.pdf", SampleKeycuts.VickiName, 2);
                creator.DestinationError = DestinationError.NotFound;
                return creator;
            }
        }

        public static CreatorViewModel CreatorLongDestination
        {
            get { return NewCreator(@"C:\Users\sample\" + new string('a', 220) + ".txt", "long", 2, KeycutKind.File); }
        }

        public static CreatorViewModel CreatorSuccess
        {
            get
            {
                var creator = NewCreator(string.Empty, string.Empty, 3);
                creator.SetSuccess(SampleKeycuts.VickiName);
                return creator;
            }
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

        public static DesignManagerViewModel ManagerError
        {
            get
            {
                var manager = new DesignManagerViewModel(SampleKeycuts.Generate(0));
                manager.ShowError(@"Can't read keycuts folder ""Z:\keycuts"".");
                return manager;
            }
        }

        private static CreatorViewModel NewCreator(string destination, string name, int count, KeycutKind? kind = null)
        {
            return new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }))
            {
                Destination = destination,
                ShortcutName = name,
                DestinationKind = kind,
                KeycutCount = count
            };
        }
    }
}
#endif
