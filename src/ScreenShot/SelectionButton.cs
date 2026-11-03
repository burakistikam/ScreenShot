using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace ScreenShot;

// A toolbar button whose selected state is also available to assistive technology.
internal sealed class SelectionButton : Button
{
    private bool selected;
    internal SelectionButton() => SetResourceReference(StyleProperty, typeof(Button));
    internal bool IsSelected
    {
        get => selected;
        set
        {
            if (selected == value) return;
            var old = selected; selected = value;
            UIElementAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(
                TogglePatternIdentifiers.ToggleStateProperty,
                old ? ToggleState.On : ToggleState.Off, value ? ToggleState.On : ToggleState.Off);
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectionPeer(this);
    private sealed class SelectionPeer(SelectionButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);
        public ToggleState ToggleState => owner.IsSelected ? ToggleState.On : ToggleState.Off;
        public void Toggle()
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.RaiseEvent(new RoutedEventArgs(ClickEvent));
        }
    }
}
