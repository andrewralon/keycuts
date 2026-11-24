using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>A row in the Run box: a keycut that matches, or the "Create “name”…" row when none does.</summary>
    public sealed class RunBoxRow
    {
        public RunBoxRow(Keycut keycut)
        {
            Keycut = keycut;
            Name = keycut.Name ?? "";
            Kind = keycut.Kind;
            Target = keycut.Target ?? "";
            RunsText = keycut.Runs switch
            {
                null => "",
                1 => "1 run",
                int runs => runs + " runs"
            };
        }

        private RunBoxRow(string name)
        {
            Name = "Create “" + name + "”…";
            Target = "Opens the creator with the name filled in";
            RunsText = "";
            IsCreate = true;
        }

        /// <summary>The keycut it runs; null for the Create row.</summary>
        public Keycut? Keycut { get; }

        public string Name { get; }

        public KeycutKind Kind { get; }

        public string Target { get; }

        public string RunsText { get; }

        public bool IsCreate { get; }

        public bool IsKeycut
        {
            get { return !IsCreate; }
        }

        /// <summary>What a screen reader reads for the row: "gmail, URL, https://mail.google.com, 42 runs".</summary>
        public string AutomationName
        {
            get
            {
                return IsCreate ? Name : string.Join(", ", new[] { Name, Controls.TypeBadge.GetLabel(Kind), Target, RunsText }.Where(part => part.Length > 0));
            }
        }

        public static RunBoxRow Create(string name)
        {
            return new RunBoxRow(name);
        }
    }

    /// <summary>
    /// The Run box (Phase 6.4, docs/styleguide.md, The Run box): type a keycut's name, Return runs it. Up to
    /// <see cref="MaxRows"/> suggestions from <see cref="RunBoxMatcher"/>, the latest run ones while nothing is typed (#332); a
    /// name that matches nothing offers to create it. Tab completes the name to the selected row's; Esc closes.
    /// </summary>
    public class RunBoxViewModel : ObservableObject
    {
        public const int MaxRows = 6;

        private readonly Action<Keycut> run;

        private readonly Action<string> create;

        private readonly Action close;

        private IReadOnlyList<Keycut> keycuts = Array.Empty<Keycut>();

        private string text = string.Empty;

        private int selectedIndex = -1;

        /// <summary>Up or Down moved the selection since the text last changed, so new keycuts must not move it.</summary>
        private bool moved;

        /// <param name="run">Runs a keycut (the app: <see cref="IDesktop.RunKeycut"/>, so its own lines count the run).</param>
        /// <param name="create">Opens the creator with the name filled in.</param>
        /// <param name="close">Hides the box; the app gives the focus back to the app that had it.</param>
        public RunBoxViewModel(Action<Keycut> run, Action<string> create, Action close)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            this.close = close ?? throw new ArgumentNullException(nameof(close));
        }

        public ObservableCollection<RunBoxRow> Rows { get; } = new();

        /// <summary>
        /// The keycuts to match, as the Manager loads them (run counts included). A new list keeps what was typed, and a row
        /// chosen with Up or Down while it is still listed: the list is loaded after the box opens, and Return must not run another.
        /// </summary>
        public IReadOnlyList<Keycut> Keycuts
        {
            get { return keycuts; }
            set
            {
                var chosen = moved ? Selected?.Keycut?.Name : null;
                keycuts = value ?? Array.Empty<Keycut>();
                Refresh(chosen);
            }
        }

        public string Text
        {
            get { return text; }
            set
            {
                if (SetProperty(ref text, value ?? string.Empty))
                {
                    moved = false;
                    Refresh();
                }
            }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (SetProperty(ref selectedIndex, value))
                {
                    OnPropertyChanged(nameof(Selected));
                    OnPropertyChanged(nameof(Completion));
                }
            }
        }

        public RunBoxRow? Selected
        {
            get { return selectedIndex >= 0 && selectedIndex < Rows.Count ? Rows[selectedIndex] : null; }
        }

        public bool HasRows
        {
            get { return Rows.Count > 0; }
        }

        public bool OffersCreate
        {
            get { return Rows.Count == 1 && Rows[0].IsCreate; }
        }

        /// <summary>The hint line's word after Return: what Return does now.</summary>
        public string ReturnHint
        {
            get { return OffersCreate ? " create" : " run"; }
        }

        /// <summary>The rest of the selected keycut's name after what was typed, shown in gray after the caret, which Tab takes.</summary>
        public string Completion
        {
            get
            {
                var name = Selected is { IsKeycut: true } row ? row.Name : "";
                return text.Length > 0 && name.Length > text.Length && name.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? name.Substring(text.Length) : "";
            }
        }

        /// <summary>Opening the box: nothing typed, with the keycuts it last had, so it opens at its full height (#332); a fresh list follows.</summary>
        public void Reset()
        {
            text = string.Empty;
            moved = false;
            OnPropertyChanged(nameof(Text));
            Refresh();
        }

        /// <summary>Up and Down: the selection moves and stops at either end.</summary>
        public void Move(int by)
        {
            if (Rows.Count > 0)
            {
                moved = true;
                SelectedIndex = Math.Clamp(selectedIndex + by, 0, Rows.Count - 1);
            }
        }

        /// <summary>Tab: the name becomes the selected keycut's. False when there is nothing to complete.</summary>
        public bool Complete()
        {
            if (Selected is not { IsKeycut: true } row || string.Equals(row.Name, text, StringComparison.Ordinal))
            {
                return false;
            }

            Text = row.Name;
            return true;
        }

        /// <summary>Return: runs the selected keycut, or opens the creator for a new name, then closes. Nothing without a row.</summary>
        public void Accept()
        {
            if (Selected is not { } row)
            {
                return;
            }

            close();
            if (row.Keycut is { } keycut)
            {
                run(keycut);
                CountRun(keycut);
            }
            else
            {
                create(text.Trim());
            }
        }

        /// <summary>Esc, or a click elsewhere.</summary>
        public void Close()
        {
            close();
        }

        /// <summary>
        /// The run as its own lines will count it, so the keycut is first when the box opens again, before that list is loaded.
        /// One without those lines has no record to be in.
        /// </summary>
        private void CountRun(Keycut ran)
        {
            if (ran.CountsRuns)
            {
                keycuts = keycuts
                    .Select(keycut => ReferenceEquals(keycut, ran) ? keycut with { Runs = (keycut.Runs ?? 0) + 1, LastRun = DateTime.Now } : keycut)
                    .ToList();
            }
        }

        /// <param name="chosen">The name of the keycut to select; the first row without one, or when it isn't listed.</param>
        private void Refresh(string? chosen = null)
        {
            var typed = text.Trim();
            var found = typed.Length == 0
                ? RunBoxMatcher.Recent(keycuts, MaxRows)
                : RunBoxMatcher.Find(keycuts, typed, MaxRows).Select(match => match.Keycut);

            Rows.Clear();
            foreach (var keycut in found)
            {
                Rows.Add(new RunBoxRow(keycut));
            }

            if (Rows.Count == 0 && typed.Length > 0)
            {
                Rows.Add(RunBoxRow.Create(typed));
            }

            var kept = chosen == null ? -1 : Rows.ToList().FindIndex(row => string.Equals(row.Keycut?.Name, chosen, StringComparison.OrdinalIgnoreCase));
            selectedIndex = kept >= 0 ? kept : Rows.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(OffersCreate));
            OnPropertyChanged(nameof(ReturnHint));
            OnPropertyChanged(nameof(Completion));
        }
    }
}
