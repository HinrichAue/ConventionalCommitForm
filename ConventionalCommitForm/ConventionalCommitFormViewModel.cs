using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ConventionalCommitForm
{
    public class ConventionalCommitFormViewModel : ObservableObject, IDataErrorInfo
    {
        private List<ConventionalCommitDto> _commitHistory = new();
        private readonly string _commitHistoryFilename = "CommitHistory.xml";

        private IEnumerable<string> _types;

        public ConventionalCommitFormViewModel()
        {
            Scopes = new ObservableCollection<string>();
            Footers = new ObservableCollection<string>();

            _types = ["build", "ci", "chore", "cleanup", "debug", "docs", "feat", "fix", "perf", "refactor", "style", "test"];
            SelectedType = _types.First();

            WindowLoadedCommand = new RelayCommand(OnWindowLoaded);
            WindowClosingCommand = new RelayCommand(OnWindowClosing);
            CopyToClipboardCommand = new RelayCommand(CopyToClipboard);
            SetPreviousCommitMessageCommend =
                new RelayCommand(SetPreviousCommitMessage, CanSetPreviousCommitMessage);
            SetNextCommitMessageCommand = new RelayCommand(SetNextCommitMessage, CanSetNextCommitMessage);
        }

        public ObservableCollection<string> Scopes { get; set; }
        public ObservableCollection<string> Footers { get; set; }

        public string Scope
        {
            get;
            set => SetProperty(ref field, value);
        }

        public ICommand WindowClosingCommand { get; }
        public ICommand WindowLoadedCommand { get; }
        public ICommand CopyToClipboardCommand { get; }
        public RelayCommand SetPreviousCommitMessageCommend { get; }
        public RelayCommand SetNextCommitMessageCommand { get; }

        public IEnumerable<string> Types
        {
            get => _types;
            set => SetProperty(ref _types, value);
        }

        public string SelectedType
        {
            get;
            set
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(HeaderWidth));
            }
        }

        public bool BreakingChange
        {
            get;
            set
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(HeaderWidth));
            }
        }

        public string Body
        {
            get;
            set
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(HeaderWidth));
            }
        }

        public string Footer
        {
            get;
            set
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(HeaderWidth));
            }
        }

        public string Description
        {
            get;
            set
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(HeaderWidth));
            }
        }

        public int HeaderWidth
        {
            get
            {
                var lines = FormatCommitMessage().Split("\n");
                return lines.Any() ? lines.Select(line => line.Length).Max() : 0;
            }
        }

        public string Error => "....";

        public string this[string columnName]
        {
            get
            {
                var errorMessage = string.Empty;

                if (columnName == nameof(HeaderWidth))
                    if (HeaderWidth > 72)
                        errorMessage = "Width > 72";

                return errorMessage;
            }
        }

        private void OnWindowLoaded()
        {
            var serializer = new XmlSerializer(typeof(List<ConventionalCommitDto>));

            if (File.Exists(_commitHistoryFilename) == false)
                return;

            using Stream reader = new FileStream(_commitHistoryFilename, FileMode.Open);

            try
            {
                _commitHistory = (List<ConventionalCommitDto>) serializer.Deserialize(reader);
                if (_commitHistory != null && _commitHistory.Any())
                {
                    // Normalize commit types - replace unknown types with "feat"
                    _commitHistory = _commitHistory.Select(commit => 
                        new ConventionalCommitDto(
                            ValidateCommitType(commit.Type),
                            commit.BreakingChange,
                            commit.Scope,
                            commit.Description,
                            commit.Body,
                            commit.Footer
                        )).ToList();

                    var lastCommit = _commitHistory.First();
                    InitUiFromCommitMessage(lastCommit);
                }
                else
                {
                    _commitHistory = new List<ConventionalCommitDto>();
                }
            }
            catch (Exception)
            {
                _commitHistory = new List<ConventionalCommitDto>();
            }

            SetNextCommitMessageCommand.NotifyCanExecuteChanged();
            SetPreviousCommitMessageCommend.NotifyCanExecuteChanged();

            UpdateScopes();
            UpdateFooters();
        }

        private void UpdateScopes()
        {
            var oldValue = Scope;
            Scopes.Clear();
            foreach (var scope in _commitHistory.Select(cm => cm.Scope).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                Scopes.Add(scope);
            }
            Scope = oldValue;
        }

        private void UpdateFooters()
        {
            var oldValue = Footer;
            Footers.Clear();
            foreach (var footer in _commitHistory.Select(cm => cm.Footer).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct())
            {
                Footers.Add(footer);
            }
            Footer = oldValue;
        }

        private void InitUiFromCommitMessage(ConventionalCommitDto commitMessage)
        {
            SelectedType = ValidateCommitType(commitMessage.Type);
            BreakingChange = commitMessage.BreakingChange;
            Scope = commitMessage.Scope;
            Description = commitMessage.Description;
            Body = commitMessage.Body;
            Footer = commitMessage.Footer;
        }

        private string ValidateCommitType(string type)
        {
            // If type is null, empty, or not in the valid types list, default to "feat"
            if (string.IsNullOrWhiteSpace(type) || !_types.Contains(type))
            {
                return "feat";
            }
            return type;
        }

        public void CopyToClipboard()
        {
            var commitMessage = FormatCommitMessage();
            Clipboard.SetText(commitMessage);

            var currentCommitMessage = CreateConventionalCommitDtoFromUi();

            AddCommitMessageToHistoryAtBeginning(currentCommitMessage);

            UpdateScopes();
            UpdateFooters();
        }

        private void AddCommitMessageToHistoryAtBeginning(ConventionalCommitDto newCommitMessage)
        {
            SetNextCommitMessageCommand.NotifyCanExecuteChanged();
            SetPreviousCommitMessageCommend.NotifyCanExecuteChanged();

            var newCommitHistory = new List<ConventionalCommitDto> { newCommitMessage };
            newCommitHistory.AddRange(_commitHistory);
            _commitHistory = newCommitHistory.Distinct().ToList();
        }

        private void AddCommitMessageToHistoryUnique(ConventionalCommitDto newCommitMessage)
        {
            if (_commitHistory.Contains(newCommitMessage))
                return;

            SetNextCommitMessageCommand.NotifyCanExecuteChanged();
            SetPreviousCommitMessageCommend.NotifyCanExecuteChanged();

            AddCommitMessageToHistoryAtBeginning(newCommitMessage);
        }

        private string FormatCommitMessage()
        {
            var commitMessage = new StringBuilder();

            commitMessage.Append(SelectedType.Trim());
            commitMessage.Append(FormatOptionalParameter("(", Scope, ")"));
            commitMessage.Append(BreakingChange ? "!" : string.Empty);
            commitMessage.Append(": " + Description);
            commitMessage.Append(FormatOptionalParameter("\n\n", Body?.Trim(), string.Empty));
            commitMessage.Append(FormatOptionalParameter("\n\n", Footer, string.Empty));
            return commitMessage.ToString();
        }

        private string FormatOptionalParameter(string prefix, string content, string suffix)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            return $"{prefix}{content}{suffix}";
        }

        private bool CanSetNextCommitMessage()
        {
            return _commitHistory.IndexOf(CreateConventionalCommitDtoFromUi()) > 0;
        }

        private bool CanSetPreviousCommitMessage()
        {
            return _commitHistory.IndexOf(CreateConventionalCommitDtoFromUi()) < _commitHistory.Count - 1;
        }

        private void SetNextCommitMessage()
        {
            var currentCommitMessage = CreateConventionalCommitDtoFromUi();
            AddCommitMessageToHistoryUnique(currentCommitMessage);
            var currentIndex = _commitHistory.IndexOf(currentCommitMessage);
            var previousCommitMessage = _commitHistory[currentIndex - 1];
            InitUiFromCommitMessage(previousCommitMessage);

            SetNextCommitMessageCommand.NotifyCanExecuteChanged();
            SetPreviousCommitMessageCommend.NotifyCanExecuteChanged();
        }

        private void SetPreviousCommitMessage()
        {
            var currentCommitMessage = CreateConventionalCommitDtoFromUi();
            AddCommitMessageToHistoryUnique(currentCommitMessage);
            var currentIndex = _commitHistory.IndexOf(currentCommitMessage);
            var previousCommitMessage = _commitHistory[currentIndex + 1];
            InitUiFromCommitMessage(previousCommitMessage);

            SetNextCommitMessageCommand.NotifyCanExecuteChanged();
            SetPreviousCommitMessageCommend.NotifyCanExecuteChanged();
        }

        private void OnWindowClosing()
        {
            AddCommitMessageToHistoryAtBeginning(CreateConventionalCommitDtoFromUi());
            var limitedHistory = _commitHistory.Take(100).ToList();

            using TextWriter writer = new StreamWriter(_commitHistoryFilename);
            var serializer = new XmlSerializer(typeof(List<ConventionalCommitDto>));
            serializer.Serialize(writer, limitedHistory);
            writer.Close();
        }

        private ConventionalCommitDto CreateConventionalCommitDtoFromUi()
        {
            return new ConventionalCommitDto(
                SelectedType?.Trim(),
                BreakingChange,
                Scope?.Trim(),
                Description?.Trim(),
                Body?.Trim(),
                Footer?.Trim());
        }

        public record ConventionalCommitDto(
            string Type,
            bool BreakingChange,
            string Scope,
            string Description,
            string Body,
            string Footer)
        {
            public ConventionalCommitDto() : this(default, default, default, default, default, default)
            {
            }
        };
    }
}