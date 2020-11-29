﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SapphireNotes.Models;
using SapphireNotes.Utils;
using SapphireNotes.ViewModels;

namespace SapphireNotes.Services
{
    public interface INotesService
    {
        NoteViewModel Create(string name);
        void Update(string newName, NoteViewModel note);
        void Archive(NoteViewModel note);
        void Delete(NoteViewModel note);
        void SaveAll(ICollection<NoteViewModel> notes);
        NoteViewModel[] GetAll();
    }

    public class NotesService : INotesService
    {
        private const string MetadataFileName = "metadata.bin";
        private readonly string _metadataFilePath;
        private const string ArchiveDirectoryName = "archive";
        private readonly Preferences _preferences;
        private Dictionary<string, NoteMetadata> _notesMetadata;

        public NotesService(Preferences preferences)
        {
            _preferences = preferences;

#if DEBUG
            string appDataDirectory = string.Empty;
#else
            string appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sapphire Notes");
            if (!Directory.Exists(appDataDirectory))
            {
                Directory.CreateDirectory(appDataDirectory);
            }
#endif

            _metadataFilePath = Path.Combine(appDataDirectory, MetadataFileName);

            if (File.Exists(_metadataFilePath))
            {
                LoadMetadata();
            }
            else
            {
                _notesMetadata = new Dictionary<string, NoteMetadata>();
                SaveMetadata();
            }
        }

        public NoteViewModel Create(string name)
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

            return new NoteViewModel(name, path, string.Empty, new NoteMetadata());
        }

        public void Update(string newName, NoteViewModel note)
        {
            newName = newName.Trim();

            if (note.Name == newName)
            {
                return;
            }

            if (newName.Length == 0)
            {
                throw new InvalidNoteNameException("Name is required.");
            }

            var fileName = newName + ".txt";
            if (Exists(fileName))
            {
                throw new InvalidNoteNameException("A note with the same name already exists.");
            }

            var path = Path.Combine(_preferences.NotesDirectory, fileName);
            File.Move(note.FilePath, path);

            note.Name = newName;
            note.FilePath = path;
        }

        public void Archive(NoteViewModel note)
        {
            var archiveDirectory = Path.Combine(_preferences.NotesDirectory, ArchiveDirectoryName);
            if (!Directory.Exists(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
            }

            MoveToArchive(note.FilePath);
        }

        public void Delete(NoteViewModel note)
        {
            File.Delete(note.FilePath);
        }

        public void SaveAll(ICollection<NoteViewModel> notes)
        {
            _notesMetadata.Clear();

            foreach (var note in notes)
            {
                if (note.IsDirty)
                {
                    File.WriteAllText(note.FilePath, note.Text);
                }

                _notesMetadata.Add(note.Name, new NoteMetadata
                {
                    FontSize = note.FontSize,
                    FontFamily = note.FontFamily,
                    CursorPosition = note.CursorPosition
                });
            }

            SaveMetadata();         
        }

        public NoteViewModel[] GetAll()
        {
            if (Directory.Exists(_preferences.NotesDirectory))
            {
                string[] textFiles = Directory.GetFiles(_preferences.NotesDirectory, "*.txt");

                if (textFiles.Length == 0)
                {
                    var sampleNotes = CreateSampleNotes();
                    return sampleNotes;
                }
                else
                {
                    var notes = new List<NoteViewModel>(textFiles.Length);
                    foreach (string filePath in textFiles)
                    {
                        var name = Path.GetFileNameWithoutExtension(filePath);
                        var contents = File.ReadAllText(filePath);

                        NoteMetadata metadata;
                        if (_notesMetadata.ContainsKey(name))
                        {
                            metadata = _notesMetadata[name];
                        }
                        else
                        {
                            metadata = new NoteMetadata();
                            _notesMetadata.Add(name, metadata);
                        }

                        notes.Add(new NoteViewModel(name, filePath, contents, metadata));
                    }

                    var orderedByLastWrite = notes.OrderByDescending(x => File.GetLastWriteTime(x.FilePath)).ToArray();
                    return orderedByLastWrite;
                }
            }
            else
            {
                Directory.CreateDirectory(_preferences.NotesDirectory);

                var sampleNotes = CreateSampleNotes();
                return sampleNotes;
            }
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

        private void MoveToArchive(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var archivePath = Path.Combine(_preferences.NotesDirectory, ArchiveDirectoryName, fileName);

            archivePath = FileUtil.NextAvailableFileName(archivePath);
            File.Move(filePath, archivePath);
        }

        private NoteViewModel[] CreateSampleNotes()
        {
            var note1name = "note 1";
            var note1path = Path.Combine(_preferences.NotesDirectory, note1name + ".txt");
            var note1Text = "This is a sample note";
            using (var stream = File.CreateText(note1path))
            {
                stream.Write(note1Text);
            }

            var note2name = "note 2";
            var note2path = Path.Combine(_preferences.NotesDirectory, note2name + ".txt");
            var note2Text = "This is another sample note";
            using (var stream = File.CreateText(note2path))
            {
                stream.Write(note2Text);
            }

            return new NoteViewModel[]
            {
                 new NoteViewModel(note1name, note1path, note1Text, new NoteMetadata()),
                 new NoteViewModel(note2name, note2path, note2Text, new NoteMetadata())
            };
        }

        private void LoadMetadata()
        {
            using var reader = new BinaryReader(File.Open(_metadataFilePath, FileMode.Open));
            int notesCount = reader.ReadInt32();
            _notesMetadata = new Dictionary<string, NoteMetadata>(notesCount);

            for (var i = 0; i < notesCount; i++)
            {
                var noteName = reader.ReadString();
                var metadata = new NoteMetadata
                {
                    FontSize = reader.ReadInt32(),
                    FontFamily = reader.ReadString(),
                    CursorPosition = reader.ReadInt32()
                };

                _notesMetadata.Add(noteName, metadata);
            }
        }

        private void SaveMetadata()
        {
            using var writer = new BinaryWriter(File.Open(_metadataFilePath, FileMode.OpenOrCreate));

            writer.Write(_notesMetadata.Count);

            foreach (var kvp in _notesMetadata)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.FontSize);
                writer.Write(kvp.Value.FontFamily);
                writer.Write(kvp.Value.CursorPosition);
            }
        }
    }

    public class InvalidNoteNameException : Exception
    {
        public InvalidNoteNameException(string message) : base(message) { }
    }
}
