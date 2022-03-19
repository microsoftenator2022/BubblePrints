﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlueprintExplorer
{
    public partial class BlueprintViewer : UserControl
    {
        public delegate void BlueprintHandleDelegate(BlueprintHandle bp);
        public event BlueprintHandleDelegate OnLinkOpenNewTab;
        public event BlueprintHandleDelegate OnBlueprintShown;
        public event BlueprintHandleDelegate OnOpenExternally;
        public event Action OnClose;

        public bool CanClose
        {
            set
            {
                this.close.Enabled = value;
            }
        }

        public void Navigate(NavigateTo to)
        {
            int target = to switch
            {
                NavigateTo.RelativeBackOne => ActiveHistoryIndex - 1,
                NavigateTo.RelativeForwardOne => ActiveHistoryIndex + 1,
                NavigateTo.AbsoluteFirst => 0,
                NavigateTo.AbsoluteLast => history.Count - 1,
                _ => throw new NotImplementedException(),
            };

            if (target >= 0 && target < history.Count)
            {
                ActiveHistoryIndex = target;
                ShowBlueprint(history[target], 0);
                InvalidateHistory();
            }
        }

        public BlueprintViewer()
        {
            InitializeComponent();
            Form1.InstallReadline(filter);
            view.OnLinkClicked += (link, newTab) =>
            {
                if (BlueprintDB.Instance.Blueprints.TryGetValue(Guid.Parse(link), out var bp))
                {
                    if (newTab)
                        OnLinkOpenNewTab?.Invoke(bp);
                    else
                        ShowBlueprint(bp, ShowFlags.F_UpdateHistory);
                }
            };

            view.OnPathHovered += path =>
            {
                currentPath.Text = path ?? "-";
            };

            view.OnFilterChanged += filterValue =>
            {
                filter.Text = filterValue;
            };

            view.OnNavigate += Navigate;

            filter.TextChanged += (sender, e) => view.Filter = filter.Text;
            if (Form1.Dark)
            {
                BubbleTheme.DarkenControls(view, filter, references, openExternal, currentPath);
                BubbleTheme.DarkenStyles(references.DefaultCellStyle, references.ColumnHeadersDefaultCellStyle);
            }

            references.CellClick += (sender, e) => ShowReferenceSelected();

            openExternal.Click += (sender, e) => OnOpenExternally?.Invoke(View.Blueprint as BlueprintHandle);

            this.AddMouseClickRecursively(HandleXbuttons);
        }
        private void HandleXbuttons(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.XButton1)
                Navigate(NavigateTo.RelativeBackOne);
            else if (e.Button == MouseButtons.XButton2)
                Navigate(NavigateTo.RelativeForwardOne);
        }

        public void ShowBlueprint(BlueprintHandle handle, ShowFlags flags)
        {
            if (handle == view.Blueprint)
                return;

            view.Blueprint = handle;

            references.Rows.Clear();
            var me = Guid.Parse(handle.GuidText);
            foreach (var reference in handle.BackReferences)
            {
                if (reference != me)
                    references.Rows.Add(BlueprintDB.Instance.Blueprints[reference].Name);
            }

            if (flags.ClearHistory())
            {
                historyBread.Controls.Clear();
                history.Clear();
                ActiveHistoryIndex = 0;
            }

            if (flags.UpdateHistory())
                PushHistory(handle);

            OnBlueprintShown?.Invoke(handle);
        }

        public BlueprintControl View => view;

        private readonly List<BlueprintHandle> history = new();
        private int ActiveHistoryIndex = 0;

        private void PushHistory(BlueprintHandle bp) {

            for (int i = ActiveHistoryIndex + 1; i < history.Count; i++)
            {
                history.RemoveAt(ActiveHistoryIndex + 1);
                historyBread.Controls.RemoveAt(ActiveHistoryIndex + 1);
            }

            var button = new Button();
            int historyIndex = history.Count;
            if (Form1.Dark)
                BubbleTheme.DarkenControls(button);
            button.MinimumSize = new Size(10, 44);
            button.Text = bp.Name;
            button.AutoSize = true;
            int here = historyBread.Controls.Count;
            button.Click += (sender, e) => {
                ActiveHistoryIndex = historyIndex;
                ShowBlueprint(bp, 0);
                InvalidateHistory();
            };
            historyBread.Controls.Add(button);
            history.Add(bp);

            ActiveHistoryIndex = historyIndex;
            InvalidateHistory();
        }

        private void InvalidateHistory()
        {
            for (int i = 0; i < history.Count; i++) {
                var button = historyBread.Controls[i];
                if (i == ActiveHistoryIndex)
                {
                    button.Font = new Font(button.Font, FontStyle.Bold);
                }
                else
                {
                    button.Font = new Font(button.Font, FontStyle.Italic);
                }
            }
        }

        private void ShowReferenceSelected()
        {
            var handle = View.Blueprint as BlueprintHandle;
            if (handle == null) return;
            if (handle.BackReferences.Count != references.RowCount) return;
            int row = references.SelectedRow();

            if (row >= 0 && row < handle.BackReferences.Count)
                ShowBlueprint(BlueprintDB.Instance.Blueprints[handle.BackReferences[row]], ShowFlags.F_UpdateHistory);
        }

        [Flags]
        public enum ShowFlags
        {
            F_UpdateHistory = 1,
            F_ClearHistory = 2,
        }

        private void close_Click(object sender, EventArgs e) => OnClose?.Invoke();
    }
}
