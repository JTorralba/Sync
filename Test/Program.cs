﻿using System.Collections.Concurrent;

Standard.File _File = new Standard.File();
Standard.Cryptology _Cryptology = new Standard.Cryptology();

BlockingCollection<String> Queue_FileEventAll;
BlockingCollection<FileEvent> Queue_FileEvent;

ConcurrentDictionary<String, FileMemo> Dictionary_FileMemo;

Queue_FileEventAll = new BlockingCollection<String>();
Queue_FileEvent = new BlockingCollection<FileEvent>();

Dictionary_FileMemo = new ConcurrentDictionary<String, FileMemo>();

var _FileSystemWatcher = new FileSystemWatcher(@"C:\Users\JTorralba\Desktop", filter: "*");

_FileSystemWatcher.Created += Created;
_FileSystemWatcher.Changed += Changed;
_FileSystemWatcher.Deleted += Deleted;
_FileSystemWatcher.Renamed += Renamed;

_FileSystemWatcher.EnableRaisingEvents = true;
_FileSystemWatcher.IncludeSubdirectories = true;

Task.Run(() => StartProcessingFileChanges());
Task.Run(() => StartProcessingToDo());

String _Line;

do
{
    _Line = Console.ReadLine();
    if (_Line != null)
    {
        switch (_Line)
        {
            case "FE":
                foreach (var item in Queue_FileEvent)
                {
                    try
                    {
                        Console.WriteLine("{0} {1}", item.Action, item.FullPath);
                    }
                    catch (Exception E)
                    {

                    }
                }
                break;
            case "FM":
                foreach (var Key in Dictionary_FileMemo.Keys.Order())
                {
                    try
                    {
                        Console.WriteLine("{0}: {1}", Key, String.Join(", ", Dictionary_FileMemo[Key].Hash));
                    }
                    catch (Exception E)
                    {

                    }
                }
                break;
            default:
                break;
        }
    }
} while (_Line != null);

void Created(Object source, FileSystemEventArgs e)
{
    Queue_FileEventAll.Add(e.FullPath);
}

void Changed(Object source, FileSystemEventArgs e)
{
    Queue_FileEventAll.Add(e.FullPath);
}

void Deleted(Object source, FileSystemEventArgs e)
{
    Queue_FileEvent.Add(new FileEvent(e.FullPath, "D", ""));
}

void Renamed(Object source, RenamedEventArgs e)
{
    foreach (var item in Queue_FileEventAll)
    {
        bool _InQueue = false;

        String _ReQueue = "";

        if (item.Contains(e.OldFullPath + "\\"))
        {
            _InQueue = true;
            _ReQueue = item.Replace(e.OldFullPath + "\\", e.FullPath + "\\");
        }
        else
        {
            if (item == e.OldFullPath)
            {
                _InQueue = true;
                _ReQueue = e.FullPath;
            }
        }
        
        if (_InQueue)
        {
            Queue_FileEventAll.Add(_ReQueue);
        }
    }

    Queue_FileEvent.Add(new FileEvent(e.OldFullPath, "R", e.FullPath));
}

void StartProcessingFileChanges()
{
    while (!Queue_FileEventAll.IsCompleted)
    {
        String _FullPath = Queue_FileEventAll.Take();

        //Console.WriteLine("AAA: Take() -> {0}", _FullPath);

        FileInfo _FileInfo = new FileInfo(_FullPath);

        String _FileHash = "";
        long _FileSize = 0;
        DateTime _FileModified = DateTime.Now;
        FileMemo _FileMemo;

        if (!_FileInfo.Exists && !_File.IsLocked(_FullPath))
        {
            continue;
        }
        else
        {
            if (_File.IsDirectory(_FullPath))
            {
                _FileHash = new String('-', 32);
                _FileSize = 0;
                _FileModified = _FileInfo.CreationTime;
            }
            else
            {
                if (_FileInfo.Exists)
                {
                    _FileHash = _Cryptology.FileHash(_FullPath);
                    _FileSize = _FileInfo.Length;
                    _FileModified = _FileInfo.LastAccessTime;
                }
                else
                {
                    continue;
                }
            }
        }

        _FileMemo = new FileMemo(_FileHash, _FileSize, _FileModified);

        //Console.WriteLine("{0, -1} {1, -32} {2, -10} {3, -49} {4}", " ", _FileHash, _FileSize.ToString(), _FullPath, _FileModified);

        if (Dictionary_FileMemo.TryGetValue(_FullPath, out FileMemo _Record))
        {
            if (_Record != null)
            {
                if ((_FileHash != _Record.Hash) && (_FileSize != 0))
                {
                    Dictionary_FileMemo.AddOrUpdate(_FullPath, _FileMemo, (Key, OldValue) => _FileMemo);
                    Queue_FileEvent.Add(new FileEvent(_FullPath, "C", ""));
                }
                else
                {
                }
            }
            else
            {
                if (_File.IsDirectory(_FullPath))
                {
                    Dictionary_FileMemo.AddOrUpdate(_FullPath, _FileMemo, (Key, OldValue) => _FileMemo);
                    Queue_FileEvent.Add(new FileEvent(_FullPath, "C", ""));
                }
            }
        }
        else
        {
            Dictionary_FileMemo.AddOrUpdate(_FullPath, _FileMemo, (Key, OldValue) => _FileMemo);
            Queue_FileEvent.Add(new FileEvent(_FullPath, "C", ""));
        }
    }
}

void StartProcessingToDo()
{
    while (!Queue_FileEvent.IsCompleted)
    {
        FileEvent _FileEvent = Queue_FileEvent.Take();

        //Console.WriteLine("BBB: Take() -> {0} {1}", _FileEvent.Action, _FileEvent.FullPath);

        String _Hash = new String('-', 32);
        long _Size = 0;
        DateTime _Modified  = DateTime.Now;
        String _FullPathNew = "";

        if (_FileEvent.Action == "R")
        {
            String[] Split = _FileEvent.FullPathNew.Split("\\");
            String Base = String.Join("\\", Split.Take(Split.Length - 1));
            _FullPathNew = "-> " + Split.Last();
        }

        FileInfo _FileInfo = new FileInfo(_FileEvent.FullPath);

        if (!_FileInfo.Exists && !_File.IsLocked(_FileEvent.FullPath))
        {
            continue;
        }
        else
        {
            if (_File.IsDirectory(_FileEvent.FullPath))
            {
            }
            else
            {
                if (_FileInfo.Exists)
                {
                    Dictionary_FileMemo.TryGetValue(_FileEvent.FullPath, out FileMemo _X);
                    FileMemo _Y = new FileMemo(_X.Hash, _X.Size, _FileInfo.LastWriteTime);
                    Dictionary_FileMemo.AddOrUpdate(_FileEvent.FullPath, _Y, (Key, OldValue) => _Y);
                }
            }
        }

        FileMemo _Record = null;

        if (_FileEvent.Action == "R")
        {
            if (Dictionary_FileMemo.TryGetValue(_FileEvent.FullPathNew, out _Record))
            {
                _Hash = _Record.Hash;
                _Size = _Record.Size;
                _Modified = _Record.Modified;
            }
        }
        else
        {
            if (Dictionary_FileMemo.TryGetValue(_FileEvent.FullPath, out _Record))
            {
                _Hash = _Record.Hash;
                _Size = _Record.Size;
                _Modified = _Record.Modified;
            }
        }

        Console.WriteLine("{0, -1} {1, -32} {2, -10} {3, -49} {4} {5}", _FileEvent.Action, _Hash, _Size.ToString(), _FileEvent.FullPath, _Modified, _FullPathNew);

        switch (_FileEvent.Action)
        {
            case "D":
                List<String> ToDelete = Dictionary_FileMemo.Keys.Where(Key => Key.Contains(_FileEvent.FullPath)).ToList();
                ToDelete.ForEach(Key => Dictionary_FileMemo.TryRemove(Key, out FileMemo _Remove));
                break;
            case "R":
                if (_File.IsDirectory(_FileEvent.FullPathNew))
                {
                    List<String> ToRename = Dictionary_FileMemo.Keys.Where(Key => Key.Contains(_FileEvent.FullPath + "\\")).ToList();
                    ToRename.ForEach(Key => Rename(Key, _FileEvent.FullPath, _FileEvent.FullPathNew));
                }
                Dictionary_FileMemo.TryGetValue(_FileEvent.FullPath, out  _Record);
                Dictionary_FileMemo.TryAdd(_FileEvent.FullPathNew, _Record);
                Dictionary_FileMemo.TryRemove(_FileEvent.FullPath, out FileMemo _Remove);
                break;
            case "C":
                break;
            default:
                break;
        }
    }

    Console.WriteLine();
}

void Rename(String _Key, String _FullPath, String _FullPathNew)
{
    Dictionary_FileMemo.TryGetValue(_Key, out FileMemo _Record);
    Dictionary_FileMemo.TryAdd(_Key.Replace(_FullPath, _FullPathNew), _Record);
    Dictionary_FileMemo.TryRemove(_Key, out FileMemo _Remove);
}

public class FileEvent
{
    public String FullPath;
    public String Action;
    public String FullPathNew;

    public FileEvent(String _FullPath, String _Action, String _FullPathNew)
    {
        this.FullPath = _FullPath;
        this.Action = _Action;
        this.FullPathNew = _FullPathNew;
    }
}

public class FileMemo
{
    public String Hash;
    public long Size;
    public DateTime Modified;

    public FileMemo(String _Hash, long _Size, DateTime _Modified)
    {
        this.Hash = _Hash;
        this.Size = _Size;
        this.Modified = _Modified;
    }
}