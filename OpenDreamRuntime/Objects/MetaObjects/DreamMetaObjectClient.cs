using OpenDreamRuntime.Procs;
using OpenDreamRuntime.Rendering;
using OpenDreamShared.Dream;

namespace OpenDreamRuntime.Objects.MetaObjects {
    sealed class DreamMetaObjectClient : IDreamMetaObject {
        public bool ShouldCallNew => true;
        public IDreamMetaObject? ParentType { get; set; }

        private readonly Dictionary<DreamList, DreamObject> _screenListToClient = new();

        private readonly IDreamManager _dreamManager = IoCManager.Resolve<IDreamManager>();

        public void OnObjectCreated(DreamObject dreamObject, DreamProcArguments creationArguments) {
            ParentType?.OnObjectCreated(dreamObject, creationArguments);

            _dreamManager.Clients.Add(dreamObject);

            ClientPerspective perspective = (ClientPerspective)dreamObject.GetVariable("perspective").GetValueAsInteger();
            if (perspective != ClientPerspective.Mob) {
                //Runtime.StateManager.AddClientPerspectiveDelta(connection.CKey, perspective);
            }
        }

        public void OnObjectDeleted(DreamObject dreamObject) {
            ParentType?.OnObjectDeleted(dreamObject);
            _dreamManager.Clients.Remove(dreamObject);
        }

        public void OnVariableSet(DreamObject dreamObject, string varName, DreamValue value, DreamValue oldValue) {
            ParentType?.OnVariableSet(dreamObject, varName, value, oldValue);

            switch (varName) {
                case "eye": {
                    string ckey = dreamObject.GetVariable("ckey").GetValueAsString();
                    DreamObject eye = value.GetValueAsDreamObject();

                    //Runtime.StateManager.AddClientEyeIDDelta(ckey, eyeID);
                    break;
                }
                case "perspective": {
                    string ckey = dreamObject.GetVariable("ckey").GetValueAsString();

                    //Runtime.StateManager.AddClientPerspectiveDelta(ckey, (ClientPerspective)variableValue.GetValueAsInteger());
                    break;
                }
                case "mob": {
                    DreamConnection connection = _dreamManager.GetConnectionFromClient(dreamObject);

                    connection.MobDreamObject = value.GetValueAsDreamObject();
                    break;
                }
                case "screen": {
                    if (oldValue.TryGetValueAsDreamList(out DreamList oldList)) {
                        oldList.Cut();
                        oldList.ValueAssigned -= ScreenValueAssigned;
                        oldList.BeforeValueRemoved -= ScreenBeforeValueRemoved;
                        _screenListToClient.Remove(oldList);
                    }

                    DreamList screenList;
                    if (!value.TryGetValueAsDreamList(out screenList)) {
                        screenList = DreamList.Create();
                    }

                    screenList.ValueAssigned += ScreenValueAssigned;
                    screenList.BeforeValueRemoved += ScreenBeforeValueRemoved;
                    _screenListToClient[screenList] = dreamObject;
                    break;
                }
                case "images":
                {
                    //TODO properly implement this var
                    if (oldValue.TryGetValueAsDreamList(out DreamList oldList)) {
                        oldList.Cut();
                    }

                    DreamList imageList;
                    if (!value.TryGetValueAsDreamList(out imageList)) {
                        imageList = DreamList.Create();
                    }

                    dreamObject.SetVariableValue(varName, new DreamValue(imageList));
                    break;
                }
                case "statpanel": {
                    //DreamConnection connection = Runtime.Server.GetConnectionFromClient(dreamObject);

                    //connection.SelectedStatPanel = variableValue.GetValueAsString();
                    break;
                }
            }
        }

        public DreamValue OnVariableGet(DreamObject dreamObject, string varName, DreamValue value) {
            switch (varName) {
                //TODO actually return the key
                case "key":
                case "ckey":
                    return new(_dreamManager.GetSessionFromClient(dreamObject).Name);
                case "address":
                    return new(_dreamManager.GetSessionFromClient(dreamObject).ConnectedClient.RemoteEndPoint.Address.ToString());
                case "inactivity":
                    return new DreamValue(0);
                case "timezone": {
                    //DreamConnection connection = Runtime.Server.GetConnectionFromClient(dreamObject);
                    //return new DreamValue((float)connection.ClientData.Timezone.BaseUtcOffset.TotalHours);
                    return new(0);
                }
                case "statpanel": {
                    //DreamConnection connection = Runtime.Server.GetConnectionFromClient(dreamObject);
                    //return new DreamValue(connection.SelectedStatPanel);
                    return DreamValue.Null;
                }
                case "mob":
                {
                    var connection = _dreamManager.GetConnectionFromClient(dreamObject);
                    return new DreamValue(connection.MobDreamObject);
                }
                case "connection":
                    return new DreamValue("seeker");
                default:
                    return ParentType?.OnVariableGet(dreamObject, varName, value) ?? value;
            }
        }

        public DreamValue OperatorOutput(DreamValue a, DreamValue b) {
            DreamConnection connection = _dreamManager.GetConnectionFromClient(a.GetValueAsDreamObjectOfType(DreamPath.Client));

            connection.OutputDreamValue(b);
            return new DreamValue(0);
        }

        private void ScreenValueAssigned(DreamList screenList, DreamValue screenKey, DreamValue screenValue) {
            if (screenValue == DreamValue.Null) return;

            DreamObject atom = screenValue.GetValueAsDreamObjectOfType(DreamPath.Movable);
            DreamConnection connection = _dreamManager.GetConnectionFromClient(_screenListToClient[screenList]);
            EntitySystem.Get<ServerScreenOverlaySystem>().AddScreenObject(connection, atom);
        }

        private void ScreenBeforeValueRemoved(DreamList screenList, DreamValue screenKey, DreamValue screenValue) {
            if (screenValue == DreamValue.Null) return;

            DreamObject atom = screenValue.GetValueAsDreamObjectOfType(DreamPath.Movable);
            DreamConnection connection = _dreamManager.GetConnectionFromClient(_screenListToClient[screenList]);
            EntitySystem.Get<ServerScreenOverlaySystem>().RemoveScreenObject(connection, atom);
        }
    }
}
