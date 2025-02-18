﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls;
using Cimbalino.Toolkit.Services;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Kliva.Messages;
using Kliva.Models;
using Kliva.Services.Interfaces;
using Kliva.Views;
using Microsoft.Practices.ServiceLocation;

namespace Kliva.ViewModels
{
    public class ActivityDetailViewModel : KlivaBaseViewModel
    {
        private readonly IStravaService _stravaService;

        private Activity _selectedActivity;
        public Activity SelectedActivity
        {
            get { return _selectedActivity; }
            set
            {
                Set(() => SelectedActivity, ref _selectedActivity, value);
                RaisePropertyChanged(() => KudosCount);
                RaisePropertyChanged(() => CommentCount);
                RaisePropertyChanged(() => PhotoCount);
            }
        }

        private ObservableCollection<AthleteSummary> _kudos = new ObservableCollection<AthleteSummary>();
        public ObservableCollection<AthleteSummary> Kudos
        {
            get { return _kudos; }
            set { Set(() => Kudos, ref _kudos, value); }
        } 

        private ObservableCollection<Comment> _comments = new ObservableCollection<Comment>();
        public ObservableCollection<Comment> Comments
        {
            get { return _comments; }
            set { Set(() => Comments, ref _comments, value); }
        }

        private ObservableCollection<AthleteSummary> _relatedAthletes = new ObservableCollection<AthleteSummary>();
        public ObservableCollection<AthleteSummary> RelatedAthletes
        {
            get { return _relatedAthletes; }
            set { Set(() => RelatedAthletes, ref _relatedAthletes, value); }
        } 

        private bool _hasSegments;
        public bool HasSegments
        {
            get { return _hasSegments; }
            set { Set(() => HasSegments, ref _hasSegments, value); }
        }

        private bool _hasPhotos;
        public bool HasPhotos
        {
            get { return _hasPhotos; }
            set { Set(() => HasPhotos, ref _hasPhotos, value); }
        }

        private bool _hasKudoed;
        public bool HasKudoed
        {
            get { return _hasKudoed; }
            set { Set(() => HasKudoed, ref _hasKudoed, value); }
        }

        public int KudosCount => SelectedActivity?.KudosCount ?? 0;
        public int CommentCount => SelectedActivity?.CommentCount ?? 0;
        public int PhotoCount => SelectedActivity?.TotalPhotoCount ?? 0;

        private RelayCommand _kudosCommand;
        public RelayCommand KudosCommand => _kudosCommand ?? (_kudosCommand = new RelayCommand(async () => await OnKudos()));

        private RelayCommand _mapCommand;
        public RelayCommand MapCommand => _mapCommand ?? (_mapCommand = new RelayCommand(() => NavigationService.Navigate<MapPage>(SelectedActivity?.Map)));

        private RelayCommand<ItemClickEventArgs> _athleteTappedCommand;
        public RelayCommand<ItemClickEventArgs> AthleteTappedCommand => _athleteTappedCommand ?? (_athleteTappedCommand = new RelayCommand<ItemClickEventArgs>(OnAthleteTapped));

        public ActivityDetailViewModel(INavigationService navigationService, IStravaService stravaService) : base(navigationService)
        {
            _stravaService = stravaService;
            MessengerInstance.Register<ActivitySummaryMessage>(this, async item => await LoadActivityDetails(item.ActivitySummary.Id.ToString()));
        }

        private async Task LoadActivityDetails(string activityId)
        {
            Kudos.Clear();
            Comments.Clear();
            RelatedAthletes.Clear();

            var activity = await _stravaService.GetActivityAsync(activityId, true);
            var athlete = await _stravaService.GetAthleteAsync();

            if (activity != null)
            {
                SelectedActivity = activity;

                if (activity.KudosCount > 0 && activity.Kudos != null && activity.Kudos.Any())
                {                    
                    foreach (AthleteSummary kudo in activity.Kudos)
                        Kudos.Add(kudo);
                }
                
                if (activity.CommentCount > 0 && activity.Comments != null && activity.Comments.Any())
                {
                    foreach(Comment comment in activity.Comments)
                        Comments.Add(comment);
                }
                
                if (activity.OtherAthleteCount > 0 && activity.RelatedActivities != null && activity.RelatedActivities.Any())
                {
                    foreach (ActivitySummary relatedActivity in activity.RelatedActivities)
                        RelatedAthletes.Add(relatedActivity.Athlete);
                }

                //Currently the Public API of Strava will not give us Segments info for 'other' athletes then the one logged in
                HasSegments = SelectedActivity.SegmentEfforts != null;

                //Currently the Public API of Strava will not give us the Photo links for 'other' athletes then the one logged in
                //But we do get the photo count, so we also need to verify the current user vs the one from the activity
                HasPhotos = athlete.Id == SelectedActivity.Athlete.Id && SelectedActivity.TotalPhotoCount > 0;
                HasKudoed = athlete.Id == SelectedActivity.Athlete.Id || SelectedActivity.HasKudoed;

                //TODO: Glenn - Why oh why are we not yet able to show/hide PivotItems through Visibility bindable
                ServiceLocator.Current.GetInstance<IMessenger>().Send<PivotMessage>(new PivotMessage(Pivots.Segments, HasSegments));
                ServiceLocator.Current.GetInstance<IMessenger>().Send<PivotMessage>(new PivotMessage(Pivots.Photos, HasPhotos));

                ServiceLocator.Current.GetInstance<IMessenger>()
                    .Send<ActivityPolylineMessage>(!string.IsNullOrEmpty(activity?.Map.SummaryPolyline)
                        ? new ActivityPolylineMessage(activity.Map.GeoPositions)
                        : new ActivityPolylineMessage(new List<BasicGeoposition>()));
            }
        }

        private async Task OnKudos()
        {
            await _stravaService.GiveKudosAsync(SelectedActivity.Id.ToString());
            await LoadActivityDetails(SelectedActivity.Id.ToString());
            ServiceLocator.Current.GetInstance<IMessenger>().Send<PivotMessage>(new PivotMessage(Pivots.Kudos, true, true));
        }
    }
}