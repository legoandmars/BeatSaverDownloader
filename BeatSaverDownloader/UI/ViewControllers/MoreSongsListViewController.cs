﻿using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatSaverDownloader.UI.ViewControllers
{
    public class MoreSongsListViewController : BeatSaberMarkupLanguage.ViewControllers.BSMLResourceViewController
    {
        public enum FilterMode { Search, BeatSaver, ScoreSaber }
        public enum BeatSaverFilterOptions { Latest, Hot, Rating, Downloads, Plays, Uploader }
        public enum ScoreSaberFilterOptions { Trending, RecentlyRanked, Difficulty }

        private FilterMode _currentFilter = FilterMode.BeatSaver;
        private BeatSaverFilterOptions _currentBeatSaverFilter = BeatSaverFilterOptions.Hot;
        private ScoreSaberFilterOptions _currentScoreSaberFilter = ScoreSaberFilterOptions.Trending;
        private BeatSaverSharp.User _currentUploader;
        private string _currentSearch;

        public override string ResourceName => "BeatSaverDownloader.UI.BSML.moreSongsList.bsml";
        internal NavigationController navController;
        [UIParams]
        private BeatSaberMarkupLanguage.Parser.BSMLParserParams parserParams;

        [UIComponent("list")]
        public CustomListTableData customListTableData;
        [UIComponent("loadingModal")]
        public ModalView loadingModal;

        public List<BeatSaverSharp.Beatmap> _songs = new List<BeatSaverSharp.Beatmap>();
        public LoadingControl loadingSpinner;
        internal Progress<Double> fetchProgress;

        public bool Working
        {
            get { return _working; }
            set { _working = value; if (!loadingSpinner) return; SetLoading(value); }
        }

        private bool _working;
        private uint lastPage = 0;

        [UIAction("listSelect")]
        internal void Select(TableView tableView, int row)
        {
            MoreSongsFlowCoordinator.didSelectSong?.Invoke(_songs[row], customListTableData.data[row].icon);
        }

        [UIAction("pageDownPressed")]
        internal async void PageDownPressed()
        {
            //Plugin.log.Info($"Number of cells {7}  visible cell last idx {customListTableData.tableView.visibleCells.Last().idx}  count {customListTableData.data.Count()}   math {customListTableData.data.Count() - customListTableData.tableView.visibleCells.Last().idx})");
            if (!(customListTableData.data.Count >= 1)) return;
            if ((customListTableData.data.Count() - customListTableData.tableView.visibleCells.Last().idx) <= 14)
            {
                await GetNewPage();
            }
        }

        internal void ClearData()
        {
            lastPage = 0;
            customListTableData.data.Clear();
            customListTableData.tableView.ReloadData();
            _songs.Clear();
        }
        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            base.DidDeactivate(deactivationType);
        }
        [UIAction("searchPressed")]
        internal async void SearchPressed(string text)
        {
         //   Plugin.log.Info("Search Pressed: " + text);
            _currentSearch = text;
            _currentFilter = FilterMode.Search;
            ClearData();
            await GetNewPage(2);
        }
        [UIAction("#post-parse")]
        internal async void SetupList()
        {
            (transform as RectTransform).sizeDelta = new Vector2(70, 0);
            (transform as RectTransform).anchorMin = new Vector2(0.5f, 0);
            (transform as RectTransform).anchorMax = new Vector2(0.5f, 1);
            loadingSpinner = GameObject.Instantiate(Resources.FindObjectsOfTypeAll<LoadingControl>().First(), loadingModal.transform);
            customListTableData.data.Clear();
            fetchProgress = new Progress<double>(ProgressUpdate);
            // Add items here
            await GetNewPage(2);
            // customListTableData.tableView.ScrollToCellWithIdx(InitialItem, HMUI.TableViewScroller.ScrollPositionType.Beginning, false);
            // customListTableData.tableView.SelectCellWithIdx(InitialItem);
        }

        public void ProgressUpdate(double progress)
        {
            SetLoading(true, progress);
        }

        public void SetLoading(bool value, double progress = 0)
        {
            if (value)
            {
                parserParams.EmitEvent("open-loadingModal");
                loadingSpinner.ShowDownloadingProgress("Fetching More Songs...", (float)progress);
            }
            else
            {
                parserParams.EmitEvent("close-loadingModal");
            }
        }


        internal async Task GetNewPage(uint count = 1)
        {
            if (Working) return;
            Plugin.log.Info($"Fetching {count} new page(s)");
            Working = true;
            switch (_currentFilter)
            {
                case FilterMode.BeatSaver:
                    await GetPagesBeatSaver(count);
                    break;
                //    case FilterMode.ScoreSaber:
                //       await GetPagesScoreSaber(count);
                //        break;
                case FilterMode.Search:
                    await GetPagesSearch(count);
                    break;
            }
            Working = false;
        }
        internal async Task GetPagesBeatSaver(uint count)
        {
            for (uint i = 0; i < count; ++i)
            {
                BeatSaverSharp.Page page = null;
                switch (_currentBeatSaverFilter)
                {
                    case BeatSaverFilterOptions.Hot:
                        page = await BeatSaverSharp.BeatSaver.Hot(lastPage, fetchProgress);
                        break;
                    case BeatSaverFilterOptions.Latest:
                        page = await BeatSaverSharp.BeatSaver.Latest(lastPage, fetchProgress);
                        break;
                    case BeatSaverFilterOptions.Rating:
                        page = await BeatSaverSharp.BeatSaver.Rating(lastPage, fetchProgress);
                        break;
                    case BeatSaverFilterOptions.Plays:
                        page = await BeatSaverSharp.BeatSaver.Plays(lastPage, fetchProgress);
                        break;
                    case BeatSaverFilterOptions.Uploader:
                        page = await _currentUploader.Beatmaps(lastPage);
                        break;
                    case BeatSaverFilterOptions.Downloads:
                        page = await BeatSaverSharp.BeatSaver.Downloads(lastPage, fetchProgress);
                        break;
                }
                if (page.Docs == null) continue;
                lastPage++;
                _songs.AddRange(page.Docs);
                foreach (var song in page.Docs)
                {
                    byte[] image = await song.FetchCoverImage();
                    Texture2D icon = Misc.Sprites.LoadTextureRaw(image);
                    customListTableData.data.Add(new CustomListTableData.CustomCellInfo(song.Name, song.Uploader.Username, icon));
                }
            }
            customListTableData.tableView.ReloadData();
        }
        internal async Task GetPagesSearch(uint count)
        {
            for (uint i = 0; i < count; ++i)
            {
                BeatSaverSharp.Page page = await BeatSaverSharp.BeatSaver.Search(_currentSearch, lastPage, fetchProgress);
                if (page.Docs == null) continue;
                lastPage++;
                _songs.AddRange(page.Docs);
                foreach (var song in page.Docs)
                {
                    byte[] image = await song.FetchCoverImage();
                    Texture2D icon = Misc.Sprites.LoadTextureRaw(image);
                    customListTableData.data.Add(new CustomListTableData.CustomCellInfo(song.Name, song.Uploader.Username, icon));
                }
            }
            customListTableData.tableView.ReloadData();
        }

    }
}