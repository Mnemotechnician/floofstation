using Content.Shared.Psionics.Abilities;
using Content.Client.Chat.Managers;
using Robust.Client.Player;

<<<<<<<< HEAD:Content.Client/Psionics/Telepathy/PsionicChatUpdateSystem.cs
namespace Content.Client.Psionics.Chat
========
namespace Content.Client.Chat
>>>>>>>> c00300bcd2f661215a9d605734766012c446a176:Content.Client/Chat/PsionicChatUpdateSystem.cs
{
    public sealed class PsionicChatUpdateSystem : EntitySystem
    {
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PsionicComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<PsionicComponent, ComponentRemove>(OnRemove);
        }

        public PsionicComponent? Player => CompOrNull<PsionicComponent>(_playerManager.LocalPlayer?.ControlledEntity);
        public bool IsPsionic => Player != null;

        private void OnInit(EntityUid uid, PsionicComponent component, ComponentInit args)
        {
            _chatManager.UpdatePermissions();
        }

        private void OnRemove(EntityUid uid, PsionicComponent component, ComponentRemove args)
        {
            _chatManager.UpdatePermissions();
        }
    }
}
