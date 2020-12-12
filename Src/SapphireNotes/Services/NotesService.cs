﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SapphireNotes.Models;
using SapphireNotes.Utils;

namespace SapphireNotes.Services
{
    public interface INotesService
    {
        Note Create(string name, string fontFamily, int fontSize);
        Note Update(string newName, Note note);
        void Archive(Note note);
        void Delete(Note note);
        void SaveAll(IEnumerable<Note> notes);
        void SaveDirtyWithMetadata(IEnumerable<Note> notes);
        Note[] LoadAll();
        void MoveAll(string oldDirectory);
        string GetFontThatAllNotesUse();
        int? GetFontSizeThatAllNotesUse();
        void SetFontForAll(string font);
        void SetFontSizeForAll(int fontSize);
    }

    public class NotesService : INotesService
    {
        private const string ArchiveDirectoryName = "archive";

        private readonly INotesMetadataService _notesMetadataService;
        private readonly Preferences _preferences;

        public NotesService(INotesMetadataService notesMetadataService, Preferences preferences)
        {
            _notesMetadataService = notesMetadataService;
            _preferences = preferences;
        }

        public Note Create(string name, string fontFamily, int fontSize)
        {
            name = name.Trim();

            if (name.Length == 0)
            {
                throw new InvalidNoteNameException("Name is required.");
            }

            var fileName = name + ".txt";
            if (Exists(fileName))
            {
                throw new InvalidNoteNameException("A note with the same name already exists.");
            }

            var path = Path.Combine(_preferences.NotesDirectory, fileName);
            File.Create(path);

            var note = new Note(name, path, string.Empty, new NoteMetadata(fontFamily, fontSize));

            _notesMetadataService.Add(note.Name, note.Metadata);
            _notesMetadataService.Save();

            return note;
        }

        public Note Update(string newName, Note note)
        {
            newName = newName.Trim();

            if (newName.Length == 0)
            {
                throw new InvalidNoteNameException("Name is required.");
            }

            var fileName = newName + ".txt";
            if (note.Name.ToLowerInvariant() != newName.ToLowerInvariant() && Exists(fileName))
            {
                throw new InvalidNoteNameException("A note with the same name already exists.");
            }

            _notesMetadataService.Remove(note.Name);
            _notesMetadataService.Add(newName, note.Metadata);
            _notesMetadataService.Save();

            var path = Path.Combine(_preferences.NotesDirectory, fileName);
            File.Move(note.FilePath, path);

            note.Name = newName;
            note.FilePath = path;
            
            return note;
        }

        public void Archive(Note note)
        {
            var archiveDirectory = Path.Combine(_preferences.NotesDirectory, ArchiveDirectoryName);
            if (!Directory.Exists(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
            }

            var fileName = Path.GetFileName(note.FilePath);
            var archivePath = Path.Combine(_preferences.NotesDirectory, ArchiveDirectoryName, fileName);

            archivePath = FileUtil.NextAvailableFileName(archivePath);
            File.Move(note.FilePath, archivePath);

            _notesMetadataService.Remove(note.Name);
            _notesMetadataService.Save();
        }

        public void Delete(Note note)
        {
            File.Delete(note.FilePath);

            _notesMetadataService.Remove(note.Name);
            _notesMetadataService.Save();
        }

        public void SaveAll(IEnumerable<Note> notes)
        {
            foreach (var note in notes)
            {
                File.WriteAllText(note.FilePath, note.Text);
            }
        }

        public void SaveDirtyWithMetadata(IEnumerable<Note> notes)
        {
            _notesMetadataService.Clear();

            foreach (var note in notes)
            {
                if (note.IsDirty)
                {
                    File.WriteAllText(note.FilePath, note.Text);
                }

                _notesMetadataService.Add(note.Name, note.Metadata);
            }

            _notesMetadataService.Save();
        }

        public Note[] LoadAll()
        {
            _notesMetadataService.LoadOrCreate();

            if (!Directory.Exists(_preferences.NotesDirectory))
            {
                Directory.CreateDirectory(_preferences.NotesDirectory);

                var sampleNotes = CreateSampleNotes();
                return sampleNotes;
            }

            string[] textFiles = Directory.GetFiles(_preferences.NotesDirectory, "*.txt");
            if (textFiles.Length == 0)
            {
                var sampleNotes = CreateSampleNotes();
                return sampleNotes;
            }

            var notes = new List<Note>(textFiles.Length);
            foreach (string filePath in textFiles)
            {
                var name = Path.GetFileNameWithoutExtension(filePath);
                var contents = File.ReadAllText(filePath);

                NoteMetadata metadata;
                if (_notesMetadataService.Contains(name))
                {
                    metadata = _notesMetadataService.Get(name);
                }
                else
                {
                    metadata = new NoteMetadata();
                    _notesMetadataService.Add(name, metadata);
                }

                notes.Add(new Note(name, filePath, contents, metadata));
            }

            if (notes.Count != _notesMetadataService.Count)
            {
                IEnumerable<string> noteNames = notes.Select(x => x.Name);
                _notesMetadataService.RemoveMissing(noteNames);
            }

            _notesMetadataService.Save();

            var orderedByLastWrite = notes.OrderByDescending(x => File.GetLastWriteTime(x.FilePath)).ToArray();
            return orderedByLastWrite;
        }

        public void MoveAll(string oldDirectory)
        {
            string[] textFiles = Directory.GetFiles(oldDirectory, "*.txt");

            foreach (string filePath in textFiles)
            {
                var newPath = Path.Combine(_preferences.NotesDirectory, Path.GetFileName(filePath));
                File.Move(filePath, newPath);
            }

            var oldArchivePath = Path.Combine(oldDirectory, ArchiveDirectoryName);
            if (!Directory.Exists(oldArchivePath))
            {
                return;
            }

            string[] archivedTextFiles = Directory.GetFiles(oldArchivePath, "*.txt");
            if (archivedTextFiles.Length == 0)
            {
                return;
            }

            var newArchivePath = Path.Combine(_preferences.NotesDirectory, ArchiveDirectoryName);
            if (!Directory.Exists(newArchivePath))
            {
                Directory.CreateDirectory(newArchivePath);
            }

            foreach (string filePath in archivedTextFiles)
            {
                var newPath = Path.Combine(newArchivePath, Path.GetFileName(filePath));
                File.Move(filePath, newPath);
            }

            Directory.Delete(oldArchivePath);
        }

        public string GetFontThatAllNotesUse()
        {
            var fonts = _notesMetadataService.GetDistinctFonts();
            if (fonts.Length == 0)
            {
                return Globals.DefaultFontFamily;
            }

            if (fonts.Length == 1)
            {
                return fonts[0];
            }

            return null;
        }

        public int? GetFontSizeThatAllNotesUse()
        {
            var fontSizes = _notesMetadataService.GetDistinctFontSizes();
            if (fontSizes.Length == 0)
            {
                return Globals.DefaultFontSize;
            }

            if (fontSizes.Length == 1)
            {
                return fontSizes[0];
            }

            return null;
        }

        public void SetFontForAll(string font)
        {
            _notesMetadataService.SetFontForAll(font);
        }

        public void SetFontSizeForAll(int fontSize)
        {
            _notesMetadataService.SetFontSizeForAll(fontSize);
        }

        private bool Exists(string fileName)
        {
            var path = Path.Combine(_preferences.NotesDirectory, fileName);
            if (File.Exists(path))
            {
                return true;
            }

            return false;
        }

        private Note[] CreateSampleNotes()
        {
            _notesMetadataService.Clear();

            var note1name = "sample note 1";
            var note1path = Path.Combine(_preferences.NotesDirectory, note1name + ".txt");
            var note1Text = "Since you don't have any notes yet we've created a few for you.";
            using (var stream = File.CreateText(note1path))
            {
                stream.Write(note1Text);
            }

            var note2name = "sample note 2";
            var note2path = Path.Combine(_preferences.NotesDirectory, note2name + ".txt");
            var note2Text = "Another sample note.";
            using (var stream = File.CreateText(note2path))
            {
                stream.Write(note2Text);
            }

            var note1 = new Note(note1name, note1path, note1Text, new NoteMetadata());
            _notesMetadataService.Add(note1.Name, note1.Metadata);

            var note2 = new Note(note2name, note2path, note2Text, new NoteMetadata());
            _notesMetadataService.Add(note2.Name, note2.Metadata);

            _notesMetadataService.Save();

            return new Note[]
            {
                 note1,
                 note2
            };
        }
    }

    public class InvalidNoteNameException : Exception
    {
        public InvalidNoteNameException(string message) : base(message) { }
    }
}
