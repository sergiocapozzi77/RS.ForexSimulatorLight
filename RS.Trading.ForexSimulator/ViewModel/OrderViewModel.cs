// <copyright file="OrderViewModel.cs" company="Racing Solutions Ltd">
// Copyright (c) Racing Solutions Ltd</copyright>

using GalaSoft.MvvmLight;

using RS.Trading.ForexSimulator.Models;

namespace RS.Trading.ForexSimulator.ViewModel
{
    public class OrderViewModel : ViewModelBase
    {
        private double pl;

        public OrderViewModel(Order model)
        {
            this.Model = model;
        }

        public Order Model { get; }

        public double PL
        {
            get => this.pl;
            set
            {
                this.pl = value;
                this.RaisePropertyChanged();
            }
        }
    }
}