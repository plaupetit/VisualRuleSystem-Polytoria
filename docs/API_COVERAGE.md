# Polytoria API Coverage

This report is generated from public Polytoria API sources and the VRS node catalog.
Do not treat the percentages as a promise that every runtime behavior is implemented or tested.

## Sources

- Generated UTC: `2026-06-26T17:19:07.8570573+00:00`
- Docs-v2: `270a4c66f03f` (2026-05-08) from https://github.com/Polytoria/Docs-v2
- lua-definitions: `af1a720fecbe` (2026-02-26) from https://github.com/Polytoria/lua-definitions

## Summary

| Metric | Count |
| --- | ---: |
| Official API types | 157 |
| Official API enums | 36 |
| Official globals | 45 |
| VRS catalog nodes | 558 |
| API types with any VRS coverage | 32 |
| API types without VRS coverage | 125 |
| Low-confidence / inferred catalog nodes | 0 |
| Catalog nodes without API metadata | 0 |

## Coverage Percentages

| Metric | Percentage | Fraction |
| --- | ---: | ---: |
| API types with any VRS coverage | 20.38% | 32/157 |
| API types without VRS coverage | 79.62% | 125/157 |
| Direct API type coverage | 2.55% | 4/157 |
| Partial API type coverage | 17.83% | 28/157 |
| Indirect or synthetic API type coverage | 0% | 0/157 |
| Inferred API type coverage | 0% | 0/157 |
| Low-confidence / inferred catalog nodes | 0% | 0/558 |
| Catalog nodes without API metadata | 0% | 0/558 |

The main coverage percentage is type-level coverage, not member-level coverage. It does not yet count every property, method, or event separately.

## Catalog Nodes By Kind

| Kind | Count |
| --- | ---: |
| Action | 162 |
| Condition | 112 |
| Property | 181 |
| Trigger | 103 |

## Type Coverage

| Coverage | Types |
| --- | ---: |
| Direct | 4 |
| Partial | 28 |
| Indirect or synthetic | 0 |
| Inferred from `apiType` | 0 |
| Uncovered | 125 |

Coverage categories:

- `Direct`: a node explicitly maps to a documented type/member.
- `Partial`: a node covers only part of the official behavior.
- `Indirect` / `Synthetic`: a node is a VRS workflow/helper, not a 1:1 API wrapper.
- `Inferred`: the report could not match `apiType` to an official or synthetic reference and kept it as a weak guess.
- `Uncovered`: no VRS node currently maps to that official type.

Confidence labels:

- `Explicit`: the catalog node has hand-written `apiReferences`.
- `AutoVerified`: `apiType` matched an official Docs-v2 type, method, property, event, enum, or global.
- `AutoClassified`: `apiType` was intentionally classified as a Lua primitive or VRS helper, not official Polytoria API coverage.
- `Mixed`: the type row combines multiple confidence levels.
- `Low` / `Inferred`: the node needs manual annotation or correction.

## Covered Types

| API type | Coverage | Confidence | VRS nodes |
| --- | --- | --- | --- |
| `BodyPosition` | Partial | AutoVerified | ACT_SetBodyPositionAcceptanceDistance (Set Body Position Stop Distance)<br>ACT_SetBodyPositionForce (Set Body Position Force)<br>ACT_SetBodyPositionTarget (Set Body Position Target)<br>COND_BodyPositionForceAtLeast (Body Position Force At Least)<br>COND_BodyPositionReachedTarget (Body Position Reached Target)<br>EV_OnBodyPositionReachedTarget (On Body Position Reached Target)<br>PROP_BodyPositionAcceptanceDistance (Body Position Stop Distance)<br>PROP_BodyPositionDistanceToTarget (Body Position Distance To Target)<br>... 2 more |
| `BoolValue` | Partial | AutoVerified | ACT_SetState (Set State)<br>ACT_ToggleState (Toggle State)<br>COND_StateIsFalse (State Is False)<br>COND_StateIsTrue (State Is True)<br>EV_OnStateChanged (On State Changed) |
| `ChatService` | Partial | AutoVerified | ACT_BroadcastChatMessage (Broadcast Chat Message)<br>ACT_SendChatMessageToPlayer (Send Chat Message To Player)<br>EV_OnChatMessage (On Chat Message) |
| `Color` | Partial | AutoVerified | ACT_SetObjectColor (Set Object Color)<br>PROP_AmbientColor (Ambient Color)<br>PROP_GameTeamColor (Game Team Color)<br>PROP_LightColor (Light Color)<br>PROP_ObjectColor (Object Color)<br>PROP_PlayerGameTeamColor (Player Game Team Color)<br>PROP_RandomColor (Random Color)<br>PROP_RGBColor (RGB Color) |
| `Image3D` | Partial | AutoVerified | ACT_Set3DImageColor (Set 3D Image Color)<br>ACT_Set3DImageFaceCamera (Set 3D Image Face Camera)<br>ACT_Set3DImageLighting (Set 3D Image Lighting)<br>ACT_Set3DImageShadows (Set 3D Image Shadows)<br>ACT_Set3DImageTextureOffset (Set 3D Image Texture Offset)<br>ACT_Set3DImageTextureScale (Set 3D Image Texture Scale)<br>COND_3DImageCastsShadows (3D Image Casts Shadows)<br>COND_3DImageFacesCamera (3D Image Faces Camera)<br>... 5 more |
| `InputService` | Partial | AutoVerified | COND_InputButtonDown (Input Button Down)<br>EV_OnInputButtonDown (On Input Button Down) |
| `Instance` | Partial | AutoVerified | ACT_CloneObject (Clone Object)<br>ACT_DestroyObject (Destroy Object)<br>ACT_SetObjectName (Set Object Name)<br>ACT_SetObjectParent (Set Object Parent)<br>ACT_SetObjectSpawnEnabled (Set Object Spawn Enabled)<br>COND_ObjectChildCountAtLeast (Object Child Count At Least)<br>COND_ObjectChildCountAtMost (Object Child Count At Most)<br>COND_ObjectExists (Object Exists)<br>... 11 more |
| `Light` | Partial | AutoVerified | ACT_SetLightBrightness (Set Light Brightness)<br>ACT_SetLightColor (Set Light Color)<br>ACT_SetLightShadows (Set Light Shadows)<br>ACT_SetLightShine (Set Light Shine) |
| `Lighting` | Partial | AutoVerified | ACT_SetAmbientColor (Set Ambient Color)<br>ACT_SetFogColor (Set Fog Color)<br>ACT_SetFogDistances (Set Fog Distances)<br>ACT_SetFogEnabled (Set Fog Enabled) |
| `Mesh` | Partial | AutoVerified | ACT_PlayMeshAnimation (Play Mesh Animation)<br>ACT_StopMeshAnimation (Stop Mesh Animation)<br>EV_OnMeshLoaded (On Mesh Loaded) |
| `NetworkEvent` | Direct | Explicit | ACT_SendInputEvent (Send Input Event)<br>EV_OnVrsInputEvent (On VRS Input Event) |
| `NPC` | Partial | AutoVerified | ACT_DamageNPC (Damage NPC)<br>ACT_HealNPC (Heal NPC)<br>ACT_KillNPC (Kill NPC)<br>ACT_MakeNPCJump (Make NPC Jump)<br>ACT_SetNPCHealth (Set NPC Health)<br>ACT_SetNPCJumpPower (Set NPC Jump Power)<br>ACT_SetNPCNavigationTarget (Set NPC Navigation Target)<br>ACT_SetNPCWalkSpeed (Set NPC Walk Speed)<br>... 13 more |
| `NumberRange` | Partial | AutoVerified | COND_NumberCompare (Number Compare) |
| `Part` | Partial | AutoVerified | ACT_HideObject (Hide Object)<br>ACT_MoveObjectDown (Move Object Down)<br>ACT_MoveObjectToAnotherObject (Move Object To Another Object)<br>ACT_MoveObjectUp (Move Object Up)<br>ACT_SetObjectAnchored (Set Object Anchored)<br>ACT_SetObjectCanCollide (Set Object Can Collide)<br>ACT_SetObjectDepthSize (Set Object Depth Size)<br>ACT_SetObjectHeightPosition (Set Object Height Position)<br>... 55 more |
| `Particles` | Partial | AutoVerified | ACT_BurstParticles (Burst Particles)<br>ACT_SetParticleAmount (Set Particle Amount)<br>ACT_StartParticles (Start Particles)<br>ACT_StopParticles (Stop Particles) |
| `Physical` | Direct | AutoVerified | ACT_MoveObjectWithPhysics (Move Object With Physics)<br>ACT_SetObjectSpinVelocity (Set Object Spin Velocity)<br>ACT_SetObjectVelocity (Set Object Velocity)<br>ACT_StartMovingPlatformLoop (Start Moving Platform Loop)<br>ACT_TurnObjectWithPhysics (Turn Object With Physics)<br>COND_ObjectIsMoving (Object Is Moving)<br>COND_ObjectSpeedAtLeast (Object Speed Is At Least)<br>EV_OnCheckpointTouched (On Checkpoint Touched)<br>... 8 more |
| `Player` | Direct | Mixed | ACT_KillPlayer (Kill Player)<br>ACT_MakePlayerJump (Make Player Jump)<br>ACT_RespawnPlayer (Respawn Player)<br>ACT_SetPlayerCanMove (Set Player Can Move)<br>ACT_SetPlayerGameTeam (Set Player Game Team)<br>EV_OnPlayerGameTeamChanged (On Player Game Team Changed)<br>EV_OnPlayerRespawned (On Player Respawned) |
| `PlayerDefaults` | Partial | AutoVerified | ACT_SetJumpPower (Set JumpPower)<br>ACT_SetMaxHealth (Set MaxHealth)<br>ACT_SetRespawnTime (Set RespawnTime)<br>ACT_SetSprintSpeed (Set SprintSpeed)<br>ACT_SetStamina (Set Stamina)<br>ACT_SetWalkSpeed (Set WalkSpeed) |
| `Players` | Direct | Mixed | COND_PlayerCountAtLeast (Player Count At Least)<br>COND_PlayerCountAtMost (Player Count At Most)<br>COND_PlayerExists (Player Exists)<br>EV_OnEnoughPlayers (On Enough Players)<br>EV_OnNotEnoughPlayers (On Not Enough Players)<br>EV_OnPlayerCountDroppedTo (On Player Count Dropped To)<br>EV_OnPlayerCountReached (On Player Count Reached)<br>EV_OnPlayerJoined (On Player Joined)<br>... 1 more |
| `PointLight` | Partial | AutoVerified | ACT_SetPointLightRange (Set Point Light Range) |
| `Script` | Partial | AutoVerified | ACT_RunLuauAction (Run Code Action)<br>ACT_ShowMessage (Show Message)<br>ACT_WaitSeconds (Wait Seconds)<br>EV_AfterDelay (After Delay)<br>EV_OnStart (On Start)<br>EV_OnTimerTick (On Timer Tick)<br>EV_RunLuauTrigger (Run Code Trigger) |
| `Seat` | Partial | AutoVerified | ACT_SetSeatAllowsNPCs (Set Seat Allows NPCs)<br>COND_SeatAllowsNPCs (Seat Allows NPCs)<br>COND_SeatIsOccupied (Seat Is Occupied)<br>EV_OnSeatSat (On Seat Sat)<br>EV_OnSeatVacated (On Seat Left)<br>PROP_SeatAllowsNPCs (Seat Allows NPCs)<br>PROP_SeatOccupant (Seat Occupant) |
| `Sound` | Partial | AutoVerified | ACT_PauseSound (Pause Sound)<br>ACT_PlaySound (Play Sound)<br>ACT_PlaySoundOnce (Play Sound Once)<br>ACT_SetSoundLoop (Set Sound Loop)<br>ACT_SetSoundVolume (Set Sound Volume)<br>ACT_StopSound (Stop Sound)<br>EV_OnSoundLoaded (On Sound Loaded) |
| `SpotLight` | Partial | AutoVerified | ACT_SetSpotLightAngle (Set Spot Light Angle)<br>ACT_SetSpotLightRange (Set Spot Light Range) |
| `Text3D` | Partial | AutoVerified | ACT_Set3DText (Set 3D Text)<br>ACT_Set3DTextColor (Set 3D Text Color)<br>ACT_Set3DTextFaceCamera (Set 3D Text Face Camera)<br>ACT_Set3DTextFontSize (Set 3D Text Size)<br>ACT_Set3DTextLighting (Set 3D Text Lighting)<br>ACT_Set3DTextOutlineColor (Set 3D Text Outline Color)<br>ACT_Set3DTextOutlineWidth (Set 3D Text Outline Width)<br>ACT_Set3DTextRichText (Set 3D Text Rich Text)<br>... 13 more |
| `Tool` | Partial | AutoVerified | ACT_ActivateTool (Activate Tool)<br>ACT_DeactivateTool (Deactivate Tool)<br>ACT_PlayToolAnimation (Play Tool Animation)<br>ACT_SetToolDroppable (Set Tool Droppable)<br>EV_OnToolActivated (On Tool Activated)<br>EV_OnToolDeactivated (On Tool Deactivated)<br>EV_OnToolEquipped (On Tool Equipped)<br>EV_OnToolUnequipped (On Tool Unequipped) |
| `TweenService` | Partial | AutoVerified | ACT_MoveObjectOverTime (Move Object Over Time)<br>ACT_TweenObjectColor (Animate Object Color)<br>ACT_TweenObjectPosition (Animate Object Position)<br>ACT_TweenObjectRotation (Animate Object Rotation)<br>ACT_TweenObjectScale (Animate Object Scale) |
| `UIButton` | Partial | AutoVerified | EV_OnUIButtonClicked (On UI Button Clicked) |
| `UILabel` | Partial | AutoVerified | ACT_SetUIText (Set UI Text)<br>ACT_SetUITextWrapped (Set UI Text Wrapping)<br>COND_UITextIs (UI Text Is)<br>COND_UITextIsEmpty (UI Text Is Empty)<br>COND_UITextWrapped (UI Text Wraps)<br>PROP_UIFontSize (UI Font Size)<br>PROP_UIText (UI Text)<br>PROP_UITextWrapped (UI Text Wraps) |
| `UIView` | Partial | AutoVerified | ACT_SetUIColor (Set UI Color)<br>PROP_UIColor (UI Color) |
| `ValueBase` | Partial | AutoVerified | ACT_ClearScriptVariable (Clear Script Variable)<br>ACT_IncrementScriptNumber (Increment Script Number)<br>ACT_SetScriptVariable (Set Script Variable)<br>COND_ScriptVariableExists (Script Variable Exists)<br>COND_ValueEquals (Value Equals)<br>COND_ValueIsEmpty (Value Is Empty)<br>EV_OnScriptVariableChanged (On Script Variable Changed) |
| `Vector3` | Partial | AutoVerified | ACT_AddObjectPosition (Add Object Position)<br>ACT_LookAtPosition (Look At Position)<br>ACT_MoveObject (Move Object)<br>ACT_RotateObject (Rotate Object)<br>ACT_RotateObjectContinuously (Rotate Object Continuously)<br>ACT_SetObjectPosition (Set Object Position)<br>ACT_SetObjectScale (Scale Object)<br>PROP_DirectionToObject (Direction To Object)<br>... 10 more |

## Uncovered Types

| API type | Coverage | Confidence | VRS nodes |
| --- | --- | --- | --- |
| `Accessory` | Uncovered | None |  |
| `AchievementsService` | Uncovered | None |  |
| `AddonObject` | Uncovered | None |  |
| `AddonToolItem` | Uncovered | None |  |
| `Animator` | Uncovered | None |  |
| `AssetsService` | Uncovered | None |  |
| `AudioAsset` | Uncovered | None |  |
| `BaseAsset` | Uncovered | None |  |
| `BindableEvent` | Uncovered | None |  |
| `Bounds` | Uncovered | None |  |
| `BuiltInAudioAsset` | Uncovered | None |  |
| `BuiltInFontAsset` | Uncovered | None |  |
| `Camera` | Uncovered | None |  |
| `CaptureService` | Uncovered | None |  |
| `CharacterModel` | Uncovered | None |  |
| `ClientScript` | Uncovered | None |  |
| `Clothing` | Uncovered | None |  |
| `ColorAdjustModifier` | Uncovered | None |  |
| `ColorSeries` | Uncovered | None |  |
| `ColorValue` | Uncovered | None |  |
| `CoreUIService` | Uncovered | None |  |
| `CreatorAddons` | Uncovered | None |  |
| `CreatorContextService` | Uncovered | None |  |
| `CreatorGUI` | Uncovered | None |  |
| `CreatorHistory` | Uncovered | None |  |
| `CreatorInterface` | Uncovered | None |  |
| `CreatorSelections` | Uncovered | None |  |
| `CreatorService` | Uncovered | None |  |
| `Datastore` | Uncovered | None |  |
| `DatastoreService` | Uncovered | None |  |
| `Decal` | Uncovered | None |  |
| `Dynamic` | Uncovered | None |  |
| `Entity` | Uncovered | None |  |
| `Environment` | Uncovered | None |  |
| `Explosion` | Uncovered | None |  |
| `FileLinkAsset` | Uncovered | None |  |
| `FilterService` | Uncovered | None |  |
| `Folder` | Uncovered | None |  |
| `FontAsset` | Uncovered | None |  |
| `Grabbable` | Uncovered | None |  |
| `GradientImageAsset` | Uncovered | None |  |
| `GradientSky` | Uncovered | None |  |
| `GUI` | Uncovered | None |  |
| `GUI3D` | Uncovered | None |  |
| `Hidden` | Uncovered | None |  |
| `HiddenBase` | Uncovered | None |  |
| `HttpRequestData` | Uncovered | None |  |
| `HttpResponseData` | Uncovered | None |  |
| `HttpService` | Uncovered | None |  |
| `ImageAsset` | Uncovered | None |  |
| `ImageSky` | Uncovered | None |  |
| `InputAction` | Uncovered | None |  |
| `InputActionAxis` | Uncovered | None |  |
| `InputActionButton` | Uncovered | None |  |
| `InputActionVector2` | Uncovered | None |  |
| `InputButton` | Uncovered | None |  |
| `InputButtonCollection` | Uncovered | None |  |
| `InsertService` | Uncovered | None |  |
| `InstanceValue` | Uncovered | None |  |
| `IntValue` | Uncovered | None |  |
| `Inventory` | Uncovered | None |  |
| `IOService` | Uncovered | None |  |
| `LightingModifier` | Uncovered | None |  |
| `Marker3D` | Uncovered | None |  |
| `MeshAnimationAsset` | Uncovered | None |  |
| `MeshAnimationInfo` | Uncovered | None |  |
| `MeshAsset` | Uncovered | None |  |
| `MissingInstance` | Uncovered | None |  |
| `Model` | Uncovered | None |  |
| `ModuleScript` | Uncovered | None |  |
| `NetMessage` | Uncovered | None |  |
| `NetworkedObject` | Uncovered | None |  |
| `NewServerRequestData` | Uncovered | None |  |
| `NumberValue` | Uncovered | None |  |
| `PlayerGUI` | Uncovered | None |  |
| `PolytorianModel` | Uncovered | None |  |
| `PreferencesService` | Uncovered | None |  |
| `PresenceService` | Uncovered | None |  |
| `ProceduralSky` | Uncovered | None |  |
| `PTAudioAsset` | Uncovered | None |  |
| `PTCallback` | Uncovered | None |  |
| `PTFunction` | Uncovered | None |  |
| `PTImageAsset` | Uncovered | None |  |
| `PTMeshAnimationAsset` | Uncovered | None |  |
| `PTMeshAsset` | Uncovered | None |  |
| `PTSignal` | Uncovered | None |  |
| `PTSignalConnection` | Uncovered | None |  |
| `PurchasesService` | Uncovered | None |  |
| `Quaternion` | Uncovered | None |  |
| `RayResult` | Uncovered | None |  |
| `ResourceAsset` | Uncovered | None |  |
| `RigidBody` | Uncovered | None |  |
| `ScriptService` | Uncovered | None |  |
| `ScriptSharedTable` | Uncovered | None |  |
| `ServerHidden` | Uncovered | None |  |
| `ServerScript` | Uncovered | None |  |
| `Sky` | Uncovered | None |  |
| `SocialService` | Uncovered | None |  |
| `Stat` | Uncovered | None |  |
| `Stats` | Uncovered | None |  |
| `StringValue` | Uncovered | None |  |
| `SunLight` | Uncovered | None |  |
| `Team` | Uncovered | None |  |
| `Teams` | Uncovered | None |  |
| `Temporary` | Uncovered | None |  |
| `Truss` | Uncovered | None |  |
| `TweenObject` | Uncovered | None |  |
| `UIContainer` | Uncovered | None |  |
| `UIField` | Uncovered | None |  |
| `UIFlowLayout` | Uncovered | None |  |
| `UIGridLayout` | Uncovered | None |  |
| `UIHFlow` | Uncovered | None |  |
| `UIHLayout` | Uncovered | None |  |
| `UIHVLayout` | Uncovered | None |  |
| `UIImage` | Uncovered | None |  |
| `UIScrollView` | Uncovered | None |  |
| `UITextInput` | Uncovered | None |  |
| `UIVFlow` | Uncovered | None |  |
| `UIViewport` | Uncovered | None |  |
| `UIVLayout` | Uncovered | None |  |
| `Vector2` | Uncovered | None |  |
| `Vector2Value` | Uncovered | None |  |
| `Vector3Value` | Uncovered | None |  |
| `World` | Uncovered | None |  |
| `WorldsService` | Uncovered | None |  |

## Low Confidence / Needs Annotation

No low-confidence nodes were found.

## Notes

- VRS nodes are human workflow nodes. One node can touch several API members, and one API member can require several nodes.
- The report uses GitHub-hosted Polytoria documentation sources because the rendered documentation site may be protected by browser checks.
- Regenerate this file after catalog changes or when Polytoria updates its public API documentation.
