namespace PropertyEditor;

public partial class Form1 : Form
{
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        AcceptsReturn = true,
        AcceptsTab = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Yu Gothic UI", 12F),
        HideSelection = false
    };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly ComboBox _viewBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripComboBox _encodingBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = 148 };
    private readonly OpenFileDialog _openDialog = new()
    {
        Filter = "Javaプロパティ|*.properties|すべてのファイル|*.*",
        Title = "プロパティファイルを開く"
    };
    private readonly SaveFileDialog _saveDialog = new()
    {
        Filter = "Javaプロパティ|*.properties|すべてのファイル|*.*",
        Title = "プロパティファイルを保存",
        DefaultExt = "properties"
    };

    private const string AppName = "Simple Java Property Editor";
    private string? _path;
    private bool _dirty;
    private bool _loading;
    private bool _tableView;
    private string _source = "";
    private byte[]? _fileBytes;
    private PropertiesDocument _document = new();
    private TextEncodingKind _encoding = AppSettings.Load();

    public Form1()
    {
        InitializeComponent();
        Font = new Font("Yu Gothic UI", 10F);
        Text = AppName;
        var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (exeIcon != null) Icon = exeIcon;
        ClientSize = new Size(900, 640);
        MinimumSize = new Size(640, 420);
        KeyPreview = true;
        EnableFileDrop(this);
        EnableFileDrop(_text);
        EnableFileDrop(_grid);

        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("ファイル");
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("終了", null, (_, _) => Close()));
        menu.Items.Add(fileMenu);

        var bar = Color.FromArgb(247, 245, 241);
        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = bar,
            Padding = new Padding(12, 8, 12, 8)
        };
        commands.Controls.Add(new SoftButton("開く", Color.FromArgb(232, 241, 248), Color.FromArgb(154, 186, 214), Color.FromArgb(27, 74, 115), bar, (_, _) => OpenFile()));
        commands.Controls.Add(new SoftButton("保存", Color.FromArgb(229, 244, 236), Color.FromArgb(154, 201, 176), Color.FromArgb(26, 102, 68), bar, (_, _) => SaveFile()));
        commands.Controls.Add(new SoftButton("名前を付けて保存", Color.FromArgb(251, 241, 228), Color.FromArgb(222, 190, 150), Color.FromArgb(122, 72, 24), bar, (_, _) => SaveFileAs()));
        var viewLabel = new Label
        {
            Text = "表示",
            AutoSize = true,
            Margin = new Padding(12, 6, 6, 0)
        };
        _viewBox.Items.AddRange(["表形式", "ファイル形式"]);
        _viewBox.SelectedIndex = 1;
        _viewBox.Margin = new Padding(0, 4, 0, 0);
        _viewBox.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            SetTableView(_viewBox.SelectedIndex == 0);
        };
        commands.Controls.Add(viewLabel);
        commands.Controls.Add(_viewBox);

        var status = new StatusStrip();
        status.Items.Add(_status);
        status.Items.Add(new ToolStripStatusLabel("文字コード"));
        _encodingBox.Font = new Font("Yu Gothic UI", 9F);
        _encodingBox.Items.AddRange(["UTF-8", "UTF-8（BOMつき）", "Shift_JIS"]);
        _loading = true;
        _encodingBox.SelectedIndex = (int)_encoding;
        _loading = false;
        _encodingBox.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _encodingBox.SelectedIndex < 0) return;
            ApplyEncoding((TextEncodingKind)_encodingBox.SelectedIndex);
        };
        status.Items.Add(_encodingBox);

        _text.TextChanged += (_, _) => { if (!_loading) MarkDirty(); };
        ConfigureGrid();

        Controls.Add(_grid);
        Controls.Add(_text);
        Controls.Add(commands);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        FormClosing += (_, e) =>
        {
            if (!ConfirmDiscard()) e.Cancel = true;
        };

        ShowCurrentView();
        SetStatus("ファイル形式で表示します。" + SaveHint());
    }

    private void ConfigureGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "キー", FillWeight = 35, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "値", FillWeight = 65, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.CellValueChanged += (_, _) => { if (!_loading) MarkDirty(); };
        _grid.CellBeginEdit += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].Tag is PropertyRow row && row.Kind != PropertyRowKind.Entry)
                e.Cancel = true;
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O))
        {
            OpenFile();
            return true;
        }
        if (keyData == (Keys.Control | Keys.S))
        {
            SaveFile();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void EnableFileDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        control.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                LoadPath(files[0]);
        };
    }


    private void OpenFile()
    {
        if (!ConfirmDiscard()) return;
        if (_openDialog.ShowDialog(this) != DialogResult.OK) return;
        _dirty = false;
        LoadPath(_openDialog.FileName);
    }

    private void LoadPath(string path)
    {
        if (!ConfirmDiscard()) return;
        try
        {
            _fileBytes = File.ReadAllBytes(path);
            _source = TextCodec.Decode(_fileBytes, _encoding);
            _document.Newline = _source.Contains("\r\n") ? "\r\n" : "\n";
            _document.EndsWithNewline = _source.EndsWith('\n');
            _path = path;
            _dirty = false;
            ShowCurrentView();
            Text = $"{AppName} - {Path.GetFileName(path)}";
            SetStatus($"{Path.GetFileName(path)} を開きました。{SaveHint()}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "開けませんでした", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetTableView(bool table)
    {
        SyncSourceFromView();
        _tableView = table;
        ShowCurrentView();
        SetStatus((table ? "表形式で表示しています。" : "ファイル形式で表示しています。") + SaveHint());
    }

    private void ShowCurrentView()
    {
        _loading = true;
        if (_tableView)
        {
            _document.Rows.Clear();
            if (_source.Length > 0)
                _document.Rows.AddRange(PropertiesDocument.Parse(_source));
            _grid.Rows.Clear();
            foreach (var row in _document.Rows)
            {
                var index = row.Kind switch
                {
                    PropertyRowKind.Comment => _grid.Rows.Add(PropertiesDocument.Unescape(row.Raw), ""),
                    PropertyRowKind.Entry => _grid.Rows.Add(row.Key, row.Value),
                    _ => _grid.Rows.Add("", "")
                };
                var gridRow = _grid.Rows[index];
                gridRow.Tag = row;
                if (row.Kind != PropertyRowKind.Entry)
                {
                    gridRow.DefaultCellStyle.ForeColor = Color.DimGray;
                    gridRow.DefaultCellStyle.BackColor = Color.FromArgb(247, 243, 235);
                    gridRow.ReadOnly = true;
                }
            }
            _text.Visible = false;
            _grid.Visible = true;
        }
        else
        {
            _text.Text = PropertiesDocument.DecodeUnicodeEscapes(_source);
            _grid.Visible = false;
            _text.Visible = true;
        }
        _loading = false;
    }

    private void SyncSourceFromView()
    {
        if (_tableView)
        {
            foreach (DataGridViewRow gridRow in _grid.Rows)
            {
                if (gridRow.Tag is not PropertyRow row || row.Kind != PropertyRowKind.Entry) continue;
                row.Key = Convert.ToString(gridRow.Cells["Key"].Value) ?? "";
                row.Value = Convert.ToString(gridRow.Cells["Value"].Value) ?? "";
            }
            _source = PropertiesDocument.Serialize(
                _document.Rows,
                PropertySaveMode.Utf8,
                _document.Newline,
                _document.EndsWithNewline || _document.Rows.Count > 0);
        }
        else if (_text.Visible)
        {
            _source = _text.Text;
        }
    }

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_path))
        {
            SaveFileAs();
            return;
        }
        WriteFile(_path);
    }

    private void SaveFileAs()
    {
        _saveDialog.FileName = string.IsNullOrEmpty(_path) ? "messages.properties" : Path.GetFileName(_path);
        if (_saveDialog.ShowDialog(this) != DialogResult.OK) return;
        WriteFile(_saveDialog.FileName);
    }

    private void WriteFile(string path)
    {
        try
        {
            SyncSourceFromView();
            var visible = PropertiesDocument.DecodeUnicodeEscapes(_source);
            byte[] bytes = TextCodec.Encode(visible, _encoding);
            _source = visible;
            _fileBytes = bytes;
            var backedUp = false;
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bk", overwrite: true);
                backedUp = true;
            }
            File.WriteAllBytes(path, bytes);
            _path = path;
            _dirty = false;
            Text = $"{AppName} - {Path.GetFileName(path)}";
            SetStatus(backedUp
                ? $"{Path.GetFileName(path)} を保存しました。直前の内容は {Path.GetFileName(path)}.bk に残しています。"
                : $"{Path.GetFileName(path)} を保存しました。");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存できませんでした", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        if (!Text.EndsWith('*')) Text += " *";
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var answer = MessageBox.Show(this, "保存していない変更があります。破棄しますか？", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return answer == DialogResult.Yes;
    }

    private void ApplyEncoding(TextEncodingKind kind)
    {
        if (kind == _encoding) return;
        if (_fileBytes != null && _dirty && !ConfirmDiscard())
        {
            _loading = true;
            _encodingBox.SelectedIndex = (int)_encoding;
            _loading = false;
            return;
        }
        _encoding = kind;
        AppSettings.Save(_encoding);
        if (_fileBytes != null)
        {
            _source = TextCodec.Decode(_fileBytes, _encoding);
            _document.Newline = _source.Contains("\r\n") ? "\r\n" : "\n";
            _document.EndsWithNewline = _source.EndsWith('\n');
            _dirty = false;
            if (Text.EndsWith(" *")) Text = Text[..^2];
            ShowCurrentView();
        }
        var name = TextCodec.Name(_encoding);
        SetStatus(_fileBytes != null
            ? "文字コードを" + name + "で読み直しました。" + SaveHint()
            : "文字コードを" + name + "にしました。" + SaveHint());
    }

    private string SaveHint() => "保存すると" + TextCodec.Name(_encoding) + "で書き出します。";

    private void SetStatus(string message) => _status.Text = message;
}
