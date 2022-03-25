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
using Prism.Commands;
using Prism.Mvvm;

namespace ConventionalCommitForm
{
    public class ConventionalCommitFormViewModel : BindableBase, IDataErrorInfo
    {
        private string _body;
        private List<ConventionalCommitDto> _commitHistory = new List<ConventionalCommitDto>();
        private readonly string _commitHistoryFilename = "CommitHistory.xml";
        private string _description;
        private string _footer;
        private string _scope;
        private string _selectedType;
        private bool _breakingChange;

        private IEnumerable<string> _types;

        public ConventionalCommitFormViewModel()
        {
            Scopes = new ObservableCollection<string>();
            Footers = new ObservableCollection<string>();

            _types = new[] { "fix", "feat", "docs", "refactor", "test", "chore", "build", "ci", "style", "perf", "cleanup" };
            SelectedType = _types.First();

            WindowLoadedCommand = new DelegateCommand(OnWindowLoaded);
            WindowClosingCommand = new DelegateCommand(OnWindowClosing);
            CopyToClipboardCommand = new DelegateCommand(CopyToClipboard);
            SetPreviousCommitMessageCommend =
                new DelegateCommand(SetPreviousCommitMessage, CanSetPreviousCommitMessage);
            SetNextCommitMessageCommand = new DelegateCommand(SetNextCommitMessage, CanSetNextCommitMessage);
        }

        public ObservableCollection<string> Scopes { get; set; }
        public ObservableCollection<string> Footers { get; set; }

        public string Scope
        {
            get => _scope;
            set => SetProperty(ref _scope, value);
        }

        public ICommand WindowClosingCommand { get; }
        public ICommand WindowLoadedCommand { get; }
        public ICommand CopyToClipboardCommand { get; }
        public DelegateCommand SetPreviousCommitMessageCommend { get; }
        public DelegateCommand SetNextCommitMessageCommand { get; }

        public IEnumerable<string> Types
        {
            get => _types;
            set => SetProperty(ref _types, value);
        }

        public string SelectedType
        {
            get => _selectedType;
            set
            {
                SetProperty(ref _selectedType, value);
                RaisePropertyChanged(nameof(HeaderWidth));
            }
        }
        
        public bool BreakingChange
        {
            get => _breakingChange;
            set
            {
                SetProperty(ref _breakingChange, value);
                RaisePropertyChanged(nameof(BreakingChange));
            }
        }

        public string Body
        {
            get => _body;
            set
            {
                SetProperty(ref _body, value);
                RaisePropertyChanged(nameof(HeaderWidth));
            }
        }

        public string Footer
        {
            get => _footer;
            set
            {
                SetProperty(ref _footer, value);
                RaisePropertyChanged(nameof(HeaderWidth));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                SetProperty(ref _description, value);
                RaisePropertyChanged(nameof(HeaderWidth));
            }
        }

        public int HeaderWidth => FormatCommitMessage().Split("\n").Select(line => line.Length).Max();

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
                if (_commitHistory.Any())
                {
                    var lastCommit = _commitHistory.First();
                    InitUiFromCommitMessage(lastCommit);
                }
            }
            catch
            {
                _commitHistory = new List<ConventionalCommitDto>();
            }

            SetNextCommitMessageCommand.RaiseCanExecuteChanged();
            SetPreviousCommitMessageCommend.RaiseCanExecuteChanged();

            UpdateScopes();
            UpdateFooters();
        }

        private void UpdateScopes()
        {
            var oldValue = Scope;
            Scopes.Clear();
            Scopes.AddRange(_commitHistory.Select(cm => cm.Scope).Distinct());
            Scope = oldValue;
        }

        private void UpdateFooters()
        {
            var oldValue = Footer;
            Footers.Clear();
            Footers.AddRange(_commitHistory.Select(cm => cm.Footer).Distinct());
            Footer = oldValue;
        }

        private void InitUiFromCommitMessage(ConventionalCommitDto commitMessage)
        {
            SelectedType = commitMessage.Type;
            BreakingChange = commitMessage.BreakingChange;
            Scope = commitMessage.Scope;
            Description = commitMessage.Description;
            Body = commitMessage.Body;
            Footer = commitMessage.Footer;
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
            SetNextCommitMessageCommand.RaiseCanExecuteChanged();
            SetPreviousCommitMessageCommend.RaiseCanExecuteChanged();

            var newCommitHistory = new List<ConventionalCommitDto>();
            newCommitHistory.Add(newCommitMessage);
            newCommitHistory.AddRange(_commitHistory);
            _commitHistory = newCommitHistory.Distinct().ToList();
        }

        private void AddCommitMessageToHistoryUnique(ConventionalCommitDto newCommitMessage)
        {
            if (_commitHistory.Contains(newCommitMessage))
                return;

            SetNextCommitMessageCommand.RaiseCanExecuteChanged();
            SetPreviousCommitMessageCommend.RaiseCanExecuteChanged();

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

            SetNextCommitMessageCommand.RaiseCanExecuteChanged();
            SetPreviousCommitMessageCommend.RaiseCanExecuteChanged();
        }

        private void SetPreviousCommitMessage()
        {
            var currentCommitMessage = CreateConventionalCommitDtoFromUi();
            AddCommitMessageToHistoryUnique(currentCommitMessage);
            var currentIndex = _commitHistory.IndexOf(currentCommitMessage);
            var previousCommitMessage = _commitHistory[currentIndex + 1];
            InitUiFromCommitMessage(previousCommitMessage);

            SetNextCommitMessageCommand.RaiseCanExecuteChanged();
            SetPreviousCommitMessageCommend.RaiseCanExecuteChanged();
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
            return new ConventionalCommitDto
            {
                Type = SelectedType,
                BreakingChange = BreakingChange,
                Scope = Scope,
                Body = Body?.Trim(),
                Description = Description,
                Footer = Footer
            };
        }

        public class ConventionalCommitDto : IEquatable<ConventionalCommitDto>
        {
            public string Type { get; set; }
            public bool BreakingChange { get; set; }
            public string Scope { get; set; }
            public string Description { get; set; }
            public string Body { get; set; }
            public string Footer { get; set; }

            public bool Equals(ConventionalCommitDto other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Type == other.Type && Scope == other.Scope && Description == other.Description &&
                       Body == other.Body && Footer == other.Footer;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ConventionalCommitDto) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Type, BreakingChange, Scope, Description, Body, Footer);
            }
        }
    }
}