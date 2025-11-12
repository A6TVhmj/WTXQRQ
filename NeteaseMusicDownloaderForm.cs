using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdbFileUploader
{
    public partial class NeteaseMusicDownloaderForm : Form
    {
        // 添加API相关字段
        private const string ApiBaseUrl = "https://wyapi.toubiec.cn";
        private const string SearchApiUrl = "https://apis.netstart.cn/music/search";
        private readonly HttpClient _httpClient;
        private List<SongInfo> _currentSongs = new List<SongInfo>();
        private List<SongInfo> _searchResults = new List<SongInfo>();
        private string _tempDownloadDir;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _pageSize = 20;
        private bool _isDownloading = false;

        // SongInfo类用于存储歌曲信息
        private class SongInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Artists { get; set; }
            public string Album { get; set; }
            public string Url { get; set; }
            public string PicUrl { get; set; }
            public string Quality { get; set; }
            public string Size { get; set; }
            public string FilePath { get; set; } // 本地文件路径
            public long Duration { get; set; } // 毫秒
        }

        // 搜索结果类
        private class SearchResult
        {
            public int Code { get; set; }
            public SearchData Result { get; set; }
        }

        private class SearchData
        {
            public List<SongSearchResult> Songs { get; set; }
            public bool HasMore { get; set; }
            public int SongCount { get; set; }
        }

        private class SongSearchResult
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<Artist> Artists { get; set; }
            public AlbumInfo Album { get; set; }
            public long Duration { get; set; } // 毫秒
        }

        private class Artist
        {
            public string Name { get; set; }
        }

        private class AlbumInfo
        {
            public string Name { get; set; }
            public long PublishTime { get; set; }
        }

        public NeteaseMusicDownloaderForm()
        {
            InitializeUI();
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://wyapi.toubiec.cn");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://wyapi.toubiec.cn/");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            try
            {
                this.Icon = new Icon("icon.ico");
            }
            catch
            {
                // 如果图标不存在，使用默认图标
            }

            // 创建临时下载目录
            _tempDownloadDir = Path.Combine(Path.GetTempPath(), "NeteaseMusicTemp");
            if (!Directory.Exists(_tempDownloadDir))
            {
                Directory.CreateDirectory(_tempDownloadDir);
            }

            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "网易云音乐下载器";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // 创建主布局容器
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(10),
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Percent, 50F),
                    new ColumnStyle(SizeType.Percent, 50F)
                },
                RowStyles =
                {
                    new RowStyle(SizeType.Absolute, 80F),
                    new RowStyle(SizeType.Percent, 100F),
                    new RowStyle(SizeType.Absolute, 60F)
                }
            };

            // ================ 顶部搜索区域 ================
            Panel searchPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 5, 0, 5)
            };

            TableLayoutPanel searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 2,
                Padding = new Padding(0),
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Percent, 30F),
                    new ColumnStyle(SizeType.Absolute, 100F),
                    new ColumnStyle(SizeType.Absolute, 100F),
                    new ColumnStyle(SizeType.Percent, 40F),
                    new ColumnStyle(SizeType.Absolute, 200F)
                },
                RowStyles =
                {
                    new RowStyle(SizeType.Percent, 50F),
                    new RowStyle(SizeType.Percent, 50F)
                }
            };

            // 搜索标签
            Label lblSearch = new Label
            {
                Text = "搜索音乐:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 5, 0)
            };

            // 搜索框
            TextBox txtSearch = new TextBox
            {
                Name = "txtSearch",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 5, 3)
            };

            // 搜索按钮
            Button btnSearch = new Button
            {
                Text = "搜索",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            btnSearch.Click += async (s, e) => await SearchMusicAsync();

            // 搜索结果统计
            Label lblSearchResult = new Label
            {
                Name = "lblSearchResult",
                Text = "共找到 0 首歌曲",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gray
            };

            // 分页控件
            TableLayoutPanel paginationLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(0),
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Absolute, 30F),
                    new ColumnStyle(SizeType.Absolute, 80F),
                    new ColumnStyle(SizeType.Absolute, 30F),
                    new ColumnStyle(SizeType.Percent, 100F)
                }
            };

            Button btnPrevPage = new Button
            {
                Name = "btnPrevPage",
                Text = "<",
                Enabled = false,
                Margin = new Padding(0, 3, 2, 3)
            };
            btnPrevPage.Click += async (s, e) =>
            {
                if (_currentPage > 1)
                {
                    _currentPage--;
                    await LoadSearchPageAsync();
                }
            };

            Label lblPageInfo = new Label
            {
                Name = "lblPageInfo",
                Text = "1/1",
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 3, 2, 3)
            };

            Button btnNextPage = new Button
            {
                Name = "btnNextPage",
                Text = ">",
                Enabled = false,
                Margin = new Padding(0, 3, 0, 3)
            };
            btnNextPage.Click += async (s, e) =>
            {
                if (_currentPage < _totalPages)
                {
                    _currentPage++;
                    await LoadSearchPageAsync();
                }
            };

            paginationLayout.Controls.Add(btnPrevPage, 0, 0);
            paginationLayout.Controls.Add(lblPageInfo, 1, 0);
            paginationLayout.Controls.Add(btnNextPage, 2, 0);

            // 添加搜索控件到布局
            searchLayout.Controls.Add(lblSearch, 0, 0);
            searchLayout.Controls.Add(txtSearch, 1, 0);
            searchLayout.Controls.Add(btnSearch, 2, 0);
            searchLayout.Controls.Add(lblSearchResult, 3, 0);
            searchLayout.Controls.Add(paginationLayout, 4, 0);

            // 替换输入ID提示
            Label lblIdTip = new Label
            {
                Text = "或输入歌曲ID:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 5, 0)
            };

            TextBox txtUrl = new TextBox
            {
                Name = "txtUrl",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 5, 3),
                ForeColor = Color.Gray
            };
            txtUrl.GotFocus += TxtUrl_GotFocus;
            txtUrl.LostFocus += TxtUrl_LostFocus;
            txtUrl.Text = "请输入歌曲ID";

            Button btnParse = new Button
            {
                Text = "解析ID",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            btnParse.Click += async (s, e) => await ParseUrlAsync();

            // 音质选择
            Label lblQuality = new Label
            {
                Text = "音质:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 5, 0)
            };

            ComboBox cboQuality = new ComboBox
            {
                Name = "cboQuality",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 5, 3),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboQuality.Items.AddRange(new object[] { "标准", "极高", "无损", "Hi-Res", "高清环绕" });
            cboQuality.SelectedIndex = 3; // 默认选择无损

            searchLayout.Controls.Add(lblIdTip, 0, 1);
            searchLayout.Controls.Add(txtUrl, 1, 1);
            searchLayout.Controls.Add(btnParse, 2, 1);
            searchLayout.Controls.Add(lblQuality, 3, 1);
            searchLayout.Controls.Add(cboQuality, 4, 1);

            searchPanel.Controls.Add(searchLayout);
            mainLayout.Controls.Add(searchPanel, 0, 0);
            mainLayout.SetColumnSpan(searchPanel, 2);

            // ================ 中间内容区域 ================
            // 左侧：搜索结果
            GroupBox grpSearchResults = new GroupBox
            {
                Text = "搜索结果",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 5, 0)
            };

            TableLayoutPanel searchResultsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                RowCount = 1,
                ColumnCount = 1
            };

            ListView lvSearchResults = new ListView
            {
                Name = "lvSearchResults",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            lvSearchResults.Columns.Add("歌曲", 250);
            lvSearchResults.Columns.Add("歌手", 150);
            lvSearchResults.Columns.Add("专辑", 150);
            lvSearchResults.Columns.Add("时长", 80);
            lvSearchResults.DoubleClick += async (s, e) => await AddSelectedSearchResultsToDownloadListAsync();

            searchResultsLayout.Controls.Add(lvSearchResults, 0, 0);
            grpSearchResults.Controls.Add(searchResultsLayout);
            mainLayout.Controls.Add(grpSearchResults, 0, 1);

            // 右侧：操作区域
            GroupBox grpOperations = new GroupBox
            {
                Text = "下载管理",
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 5, 0, 0)
            };

            TableLayoutPanel operationsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                RowCount = 5,
                ColumnCount = 1,
                RowStyles =
                {
                    new RowStyle(SizeType.Percent, 30F),
                    new RowStyle(SizeType.Absolute, 30F),
                    new RowStyle(SizeType.Percent, 40F),
                    new RowStyle(SizeType.Absolute, 40F),
                    new RowStyle(SizeType.Absolute, 30F)
                }
            };

            // 歌曲信息显示
            TextBox txtInfo = new TextBox
            {
                Name = "txtInfo",
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 0, 0, 5)
            };
            operationsLayout.Controls.Add(txtInfo, 0, 0);

            

            // 下载列表
            ListBox lstSongs = new ListBox
            {
                Name = "lstSongs",
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.MultiExtended,
                Margin = new Padding(0, 0, 0, 5)
            };
            operationsLayout.Controls.Add(lstSongs, 0, 2);

            // 操作按钮
            FlowLayoutPanel btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            Button btnClearList = new Button
            {
                Text = "清除列表",
                Margin = new Padding(0, 0, 5, 0)
            };
            btnClearList.Click += (s, e) => ClearSongList();

            Button btnDownload = new Button
            {
                Name = "btnDownload",
                Text = "下载选中歌曲",
                Margin = new Padding(0, 0, 5, 0)
            };
            btnDownload.Click += async (s, e) => await DownloadSelectedSongsAsync();

            Button btnCancel = new Button
            {
                Name = "btnCancel",
                Text = "取消",
                Visible = false
            };
            btnCancel.Click += (s, e) => CancelDownload();

            btnPanel.Controls.Add(btnClearList);
            btnPanel.Controls.Add(btnDownload);
            btnPanel.Controls.Add(btnCancel);
            operationsLayout.Controls.Add(btnPanel, 0, 3);

            // 状态信息
            Label lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "就绪",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gray
            };
            operationsLayout.Controls.Add(lblStatus, 0, 4);

            grpOperations.Controls.Add(operationsLayout);
            mainLayout.Controls.Add(grpOperations, 1, 1);

            // ================ 底部进度区域 ================
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 5, 0, 0)
            };

            ProgressBar progressBar = new ProgressBar
            {
                Name = "progressBar",
                Dock = DockStyle.Fill,
                Visible = false,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            bottomPanel.Controls.Add(progressBar);
            mainLayout.Controls.Add(bottomPanel, 0, 2);
            mainLayout.SetColumnSpan(bottomPanel, 2);

            this.Controls.Add(mainLayout);
        }

        private void TxtUrl_GotFocus(object sender, EventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb.Text == "请输入歌曲ID")
            {
                tb.Text = "";
                tb.ForeColor = Color.Black;
            }
        }

        private void TxtUrl_LostFocus(object sender, EventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = "请输入歌曲ID";
                tb.ForeColor = Color.Gray;
            }
        }

        private async Task SearchMusicAsync()
        {
            var txtSearch = this.Controls.Find("txtSearch", true).FirstOrDefault() as TextBox;
            var lblSearchResult = this.Controls.Find("lblSearchResult", true).FirstOrDefault() as Label;
            var lblPageInfo = this.Controls.Find("lblPageInfo", true).FirstOrDefault() as Label;
            var btnPrevPage = this.Controls.Find("btnPrevPage", true).FirstOrDefault() as Button;
            var btnNextPage = this.Controls.Find("btnNextPage", true).FirstOrDefault() as Button;
            var lvSearchResults = this.Controls.Find("lvSearchResults", true).FirstOrDefault() as ListView;
            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            var progressBar = this.Controls.Find("progressBar", true).FirstOrDefault() as ProgressBar;

            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                MessageBox.Show("请输入搜索关键词", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                lblStatus.Text = "正在搜索...";
                progressBar.Visible = true;
                progressBar.Value = 10;

                // 清空之前的搜索结果
                _searchResults.Clear();
                lvSearchResults.Items.Clear();

                // 执行搜索
                string keywords = txtSearch.Text.Trim();
                var result = await PerformSearchAsync(keywords, _currentPage, _pageSize);

                if (result == null || result.Result == null || result.Result.Songs == null || result.Result.Songs.Count == 0)
                {
                    lblStatus.Text = "未找到相关歌曲";
                    lblSearchResult.Text = "共找到 0 首歌曲";
                    _totalPages = 1;
                    _currentPage = 1;
                    lblPageInfo.Text = "1/1";
                    btnPrevPage.Enabled = false;
                    btnNextPage.Enabled = false;
                    return;
                }

                // 处理搜索结果
                _searchResults = result.Result.Songs.Select(s => new SongInfo
                {
                    Id = s.Id,
                    Name = s.Name,
                    Artists = string.Join(", ", s.Artists.Select(a => a.Name)),
                    Album = s.Album?.Name ?? "未知专辑",
                    Duration = s.Duration
                }).ToList();

                // 计算分页
                _totalPages = (int)Math.Ceiling((double)result.Result.SongCount / _pageSize);
                if (_currentPage > _totalPages) _currentPage = _totalPages;
                if (_currentPage < 1) _currentPage = 1;

                // 显示搜索结果
                foreach (var song in _searchResults)
                {
                    string duration = TimeSpan.FromMilliseconds(song.Duration).ToString(@"mm\:ss");
                    var item = new ListViewItem(new[]
                    {
                        song.Name,
                        song.Artists,
                        song.Album,
                        duration
                    });
                    lvSearchResults.Items.Add(item);
                }

                // 更新UI
                lblSearchResult.Text = $"共找到 {result.Result.SongCount} 首歌曲";
                lblPageInfo.Text = $"{_currentPage}/{_totalPages}";
                btnPrevPage.Enabled = _currentPage > 1;
                btnNextPage.Enabled = _currentPage < _totalPages;
                lblStatus.Text = $"找到 {result.Result.SongCount} 首歌曲";
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "搜索失败";
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task<SearchResult> PerformSearchAsync(string keywords, int page, int pageSize)
        {
            try
            {
                string url = $"{SearchApiUrl}?keywords={Uri.EscapeDataString(keywords)}&offset={(page - 1) * pageSize}&limit={pageSize}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<SearchResult>(content);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadSearchPageAsync()
        {
            var lblPageInfo = this.Controls.Find("lblPageInfo", true).FirstOrDefault() as Label;
            var btnPrevPage = this.Controls.Find("btnPrevPage", true).FirstOrDefault() as Button;
            var btnNextPage = this.Controls.Find("btnNextPage", true).FirstOrDefault() as Button;
            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            var progressBar = this.Controls.Find("progressBar", true).FirstOrDefault() as ProgressBar;

            try
            {
                lblStatus.Text = "加载中...";
                progressBar.Visible = true;
                progressBar.Value = 50;

                var txtSearch = this.Controls.Find("txtSearch", true).FirstOrDefault() as TextBox;
                await SearchMusicAsync();

                // 更新分页信息
                lblPageInfo.Text = $"{_currentPage}/{_totalPages}";
                btnPrevPage.Enabled = _currentPage > 1;
                btnNextPage.Enabled = _currentPage < _totalPages;
                progressBar.Value = 100;
            }
            catch
            {
                lblStatus.Text = "加载失败";
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task AddSelectedSearchResultsToDownloadListAsync()
        {
            var lvSearchResults = this.Controls.Find("lvSearchResults", true).FirstOrDefault() as ListView;
            var lstSongs = this.Controls.Find("lstSongs", true).FirstOrDefault() as ListBox;
            var cboQuality = this.Controls.Find("cboQuality", true).FirstOrDefault() as ComboBox;
            var txtInfo = this.Controls.Find("txtInfo", true).FirstOrDefault() as TextBox;
            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            var progressBar = this.Controls.Find("progressBar", true).FirstOrDefault() as ProgressBar;

            if (lvSearchResults.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加的歌曲", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                lblStatus.Text = "正在获取歌曲信息...";
                progressBar.Visible = true;
                progressBar.Value = 10;

                List<SongInfo> songsToAdd = new List<SongInfo>();

                foreach (ListViewItem item in lvSearchResults.SelectedItems)
                {
                    int index = lvSearchResults.Items.IndexOf(item);
                    if (index >= 0 && index < _searchResults.Count)
                    {
                        var song = _searchResults[index];
                        progressBar.Value = 20 + (int)((double)index / lvSearchResults.SelectedItems.Count * 60);

                        // 获取详细信息
                        var songDetail = await GetSongDetailAsync(song.Id);
                        if (songDetail != null)
                        {
                            song.Album = songDetail["album"].ToString();
                            song.PicUrl = songDetail["picimg"].ToString();
                        }

                        // 获取下载URL
                        string quality = GetQualityValue(cboQuality.SelectedIndex);
                        var urlData = await GetSongUrlAsync(song.Id, quality);
                        if (urlData != null && !string.IsNullOrEmpty(urlData["url"].ToString()))
                        {
                            song.Url = urlData["url"].ToString();
                            song.Quality = urlData["level"].ToString();
                            song.Size = $"{urlData["size"].Value<long>() / 1024 / 1024:F1} MB";
                        }

                        songsToAdd.Add(song);
                    }
                }

                // 添加到下载列表
                foreach (var song in songsToAdd)
                {
                    _currentSongs.Add(song);
                    string songInfo = $"{song.Artists} - {song.Name} ({song.Album})";
                    if (!string.IsNullOrEmpty(song.Quality))
                    {
                        songInfo += $" [{song.Quality}]";
                    }
                    lstSongs.Items.Add(songInfo);
                }

                // 显示最后添加的歌曲信息
                if (songsToAdd.Count > 0)
                {
                    var lastSong = songsToAdd.Last();
                    txtInfo.Text = $"歌曲: {lastSong.Name}\n" +
                                   $"艺术家: {lastSong.Artists}\n" +
                                   $"专辑: {lastSong.Album}\n" +
                                   $"时长: {TimeSpan.FromMilliseconds(lastSong.Duration).ToString(@"mm\:ss")}\n" +
                                   (!string.IsNullOrEmpty(lastSong.Quality) ? $"音质: {lastSong.Quality}\n大小: {lastSong.Size}" : "音质: 不可用");
                }

                lblStatus.Text = $"已添加 {songsToAdd.Count} 首歌曲到下载列表";
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "添加失败";
                MessageBox.Show($"添加歌曲失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task ParseUrlAsync()
        {
            var txtUrl = this.Controls.Find("txtUrl", true).FirstOrDefault() as TextBox;
            var cboQuality = this.Controls.Find("cboQuality", true).FirstOrDefault() as ComboBox;
            var lstSongs = this.Controls.Find("lstSongs", true).FirstOrDefault() as ListBox;
            var txtInfo = this.Controls.Find("txtInfo", true).FirstOrDefault() as TextBox;
            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            var progressBar = this.Controls.Find("progressBar", true).FirstOrDefault() as ProgressBar;

            if (string.IsNullOrWhiteSpace(txtUrl.Text) || txtUrl.Text == "请输入歌曲ID（如：435278010）")
            {
                MessageBox.Show("请输入有效的歌曲ID", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                lblStatus.Text = "正在解析...";
                progressBar.Visible = true;
                progressBar.Value = 10;
                string songId = txtUrl.Text.Trim();

                txtInfo.Text = "解析中...";

                // 获取歌曲详情
                var songDetail = await GetSongDetailAsync(songId);
                if (songDetail == null)
                {
                    lblStatus.Text = "解析失败";
                    txtInfo.Text = "错误：无法获取歌曲信息，请检查歌曲ID是否正确";
                    return;
                }

                progressBar.Value = 50;
                lblStatus.Text = "正在获取下载信息...";

                // 创建歌曲信息对象
                var song = new SongInfo
                {
                    Id = songDetail["id"].ToString(),
                    Name = songDetail["name"].ToString(),
                    Artists = songDetail["singer"].ToString(),
                    Album = songDetail["album"].ToString(),
                    PicUrl = songDetail["picimg"].ToString()
                };

                // 获取下载URL
                string quality = GetQualityValue(cboQuality.SelectedIndex);
                var urlData = await GetSongUrlAsync(song.Id, quality);
                if (urlData != null && !string.IsNullOrEmpty(urlData["url"].ToString()))
                {
                    song.Url = urlData["url"].ToString();
                    song.Quality = urlData["level"].ToString();
                    song.Size = $"{urlData["size"].Value<long>() / 1024 / 1024:F1} MB";
                }

                // 添加到列表
                _currentSongs.Add(song);

                // 更新UI
                string songInfo = $"{song.Artists} - {song.Name} ({song.Album})";
                if (!string.IsNullOrEmpty(song.Quality))
                {
                    songInfo += $" [{song.Quality}]";
                }
                lstSongs.Items.Add(songInfo);

                txtInfo.Text = $"歌曲: {song.Name}\n" +
                               $"艺术家: {song.Artists}\n" +
                               $"专辑: {song.Album}\n" +
                               $"时长: {songDetail["duration"]}\n" +
                               (!string.IsNullOrEmpty(song.Quality) ? $"音质: {song.Quality}\n大小: {song.Size}" : "音质: 不可用");

                progressBar.Value = 100;
                lblStatus.Text = "解析完成";

                // 清空输入框，准备下一次输入
                txtUrl.Text = "请输入歌曲ID（如：435278010)";
                txtUrl.ForeColor = Color.Gray;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "解析失败";
                txtInfo.Text = $"错误: {ex.Message}";
                MessageBox.Show($"解析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private void ClearSongList()
        {
            var lstSongs = this.Controls.Find("lstSongs", true).FirstOrDefault() as ListBox;
            var txtInfo = this.Controls.Find("txtInfo", true).FirstOrDefault() as TextBox;
            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;

            _currentSongs.Clear();
            lstSongs.Items.Clear();
            txtInfo.Text = "歌曲列表已清空";
            lblStatus.Text = "已清空下载列表";
        }

        private string GetQualityValue(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return "standard"; // 标准
                case 1: return "exhigh"; // 高品
                case 2: return "lossless"; // 极高
                case 3: return "hires"; // 无损
                case 4: return "jyeffect"; // Hi-Res
                default: return "lossless";
            }
        }

        private string GetQualityName(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0: return "标准";
                case 1: return "高品";
                case 2: return "极高";
                case 3: return "无损";
                case 4: return "Hi-Res";
                default: return "无损";
            }
        }

        private async Task<JObject> GetSongDetailAsync(string songId)
        {
            try
            {
                // 准备请求内容
                var requestData = new { id = songId };
                var requestJson = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(
                    requestJson,
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/api/music/detail", content);
                // 获取响应内容
                var responseContent = await response.Content.ReadAsStringAsync();
                // 检查响应是否成功
                if (response.IsSuccessStatusCode)
                {
                    var json = JsonConvert.DeserializeObject<JObject>(responseContent);
                    // 检查API返回的code
                    int code = json["code"].Value<int>();
                    string msg = json["msg"].ToString();
                    if (code == 200)
                    {
                        return json["data"] as JObject;
                    }
                }
                return null;
            }
            catch (TaskCanceledException)
            {
                // 请求超时
                Invoke(new Action(() => {
                    var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
                    lblStatus.Text = "API请求超时";
                    MessageBox.Show("API请求超时，请检查网络连接或尝试稍后重试",
                                   "请求超时", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
                return null;
            }
            catch (Exception ex)
            {
                // 捕获其他异常
                Debug.WriteLine($"[API DEBUG] 异常详情: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[API DEBUG] 异常堆栈: {ex.StackTrace}");
                Invoke(new Action(() => {
                    var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
                    lblStatus.Text = $"API调用出错: {ex.Message}";
                MessageBox.Show($"API调用出错:{ ex.Message}" +$"详细信息:{ ex.GetType().Name}" +$"请检查:1.网络连接2.API地址是否正确3.防火墙设置",
                                   "API错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));
            return null;
        }
        }

        private async Task<JObject> GetSongUrlAsync(string songId, string quality)
        {
            try
            {
                var content = new StringContent(
                    JsonConvert.SerializeObject(new
                    {
                        id = songId,
                        level = quality
                    }),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/api/music/url", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<JObject>(result);
                    if (json["code"].Value<int>() == 200 && json["data"].HasValues)
                    {
                        return json["data"][0] as JObject; // 取第一个结果
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task DownloadSelectedSongsAsync()
        {
            var lstSongs = this.Controls.Find("lstSongs", true).FirstOrDefault() as ListBox;
            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            var progressBar = this.Controls.Find("progressBar", true).FirstOrDefault() as ProgressBar;
            var btnCancel = this.Controls.Find("btnCancel", true).FirstOrDefault() as Button;
            var btnDownload = this.Controls.Find("btnDownload", true).FirstOrDefault() as Button;
            var cboQuality = this.Controls.Find("cboQuality", true).FirstOrDefault() as ComboBox;

            if (lstSongs.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要下载的歌曲", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _isDownloading = true;
                lblStatus.Text = "准备下载...";
                progressBar.Visible = true;
                progressBar.Value = 0;
                btnCancel.Visible = true;
                btnDownload.Enabled = false;
                List<string> downloadedFiles = new List<string>();
                int totalSongs = lstSongs.SelectedItems.Count;
                int currentSong = 0;

                foreach (var item in lstSongs.SelectedItems)
                {
                    currentSong++;
                    int songIndex = lstSongs.Items.IndexOf(item.ToString());
                    if (songIndex >= 0 && songIndex < _currentSongs.Count)
                    {
                        var song = _currentSongs[songIndex];
                        lblStatus.Text = $"正在下载 ({currentSong}/{totalSongs})...";

                        // 如果还没有解析出下载URL，再调用一次API
                        if (string.IsNullOrEmpty(song.Url))
                        {
                            string quality = GetQualityValue(cboQuality.SelectedIndex);
                            var urlData = await GetSongUrlAsync(song.Id, quality);
                            if (urlData != null && !string.IsNullOrEmpty(urlData["url"].ToString()))
                            {
                                song.Url = urlData["url"].ToString();
                                song.Quality = urlData["level"].ToString();
                                song.Size = $"{urlData["size"].Value<long>() / 1024 / 1024:F1} MB";
                            }
                        }

                        if (!string.IsNullOrEmpty(song.Url))
                        {
                            // 使用歌曲名-歌手作为文件名，并替换非法字符
                            bool isFlac = song.Url.IndexOf("flac", StringComparison.OrdinalIgnoreCase) >= 0;
                            string fileName;
                            if (isFlac)
                            {
                                fileName = $"{song.Name}-{song.Artists}.flac";
                            }
                            else
                            {
                                fileName = $"{song.Name}-{song.Artists}.mp3";
                            }
                            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                            string filePath = Path.Combine(_tempDownloadDir, fileName);

                            // 下载文件 - 使用真实进度
                            using (var response = await _httpClient.GetAsync(song.Url, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();
                                long totalBytes = response.Content.Headers.ContentLength ?? -1;

                                using (var contentStream = await response.Content.ReadAsStreamAsync())
                                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    var buffer = new byte[8192];
                                    long totalBytesRead = 0;
                                    int bytesRead;
                                    int lastProgressValue = 0;

                                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await fs.WriteAsync(buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;

                                        // 计算总进度：已下载文件数 + 当前文件进度
                                        double fileProgress = totalBytes != -1 ? (double)totalBytesRead / totalBytes : 0;
                                        int overallProgress = (int)((currentSong - 1 + fileProgress) / totalSongs * 100);

                                        // 避免频繁更新UI
                                        if (overallProgress > lastProgressValue)
                                        {
                                            progressBar.Value = overallProgress;
                                            lblStatus.Text = $"正在下载 ({currentSong}/{totalSongs}) - {overallProgress}%";
                                            Application.DoEvents(); // 允许UI更新
                                            lastProgressValue = overallProgress;

                                            // 检查是否取消
                                            if (!_isDownloading)
                                            {
                                                fs.Close();
                                                contentStream.Close();
                                                throw new OperationCanceledException();
                                            }
                                        }
                                    }
                                }
                            }

                            song.FilePath = filePath;
                            downloadedFiles.Add(filePath);
                        }
                        else
                        {
                            lblStatus.Text = $"跳过 {song.Name} (URL不可用)";
                        }
                    }
                }
                lblStatus.Text = "下载完成";
                progressBar.Value = 100;
                // 通知主窗体
                OnFilesDownloaded(downloadedFiles);
                MessageBox.Show($"成功下载 {downloadedFiles.Count} 首歌曲到临时目录",
                               "下载完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "下载已取消";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "下载失败";
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isDownloading = false;
                btnDownload.Enabled = true;
                btnCancel.Visible = false;
            }
        }

        private void CancelDownload()
        {
            _isDownloading = false;
        }

        public event Action<List<string>> FilesDownloaded;
        protected virtual void OnFilesDownloaded(List<string> files)
        {
            FilesDownloaded?.Invoke(files);
        }
    }
}