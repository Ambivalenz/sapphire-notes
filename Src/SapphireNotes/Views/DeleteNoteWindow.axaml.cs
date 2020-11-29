﻿using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SapphireNotes.Models;
using SapphireNotes.ViewModels;
using ReactiveUI;

namespace SapphireNotes.Views
{
    public class DeleteNoteWindow : Window
    {
        public DeleteNoteWindow()
        {
            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            var yesButton = this.FindControl<Button>("yesButton");
            yesButton.Command = ReactiveCommand.Create(YesButtonClicked);

            var noButton = this.FindControl<Button>("noButton");
            noButton.Command = ReactiveCommand.Create(NoButtonClicked);
        }

        public event EventHandler<DeletedNoteEventArgs> Deleted;

        private void YesButtonClicked()
        {
            var vm = (DeleteNoteViewModel)DataContext;

            NoteViewModel note = vm.Delete();
            Deleted.Invoke(this, new DeletedNoteEventArgs
            {
                Note = note
            });

            Close();
        }

        private void NoButtonClicked()
        {
            Close();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class DeletedNoteEventArgs : EventArgs
    {
        public NoteViewModel Note { get; set; }
    }
}
