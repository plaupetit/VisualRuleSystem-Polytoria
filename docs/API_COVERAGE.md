# Polytoria API Coverage

This report is generated from public Polytoria API sources and the VRS node catalog.
Do not treat the percentages as a promise that every runtime behavior is implemented or tested.

## Sources

- Generated UTC: `2026-06-28T15:20:14.5628069+00:00`
- Docs-v2: `270a4c66f03f` (2026-05-08) from https://github.com/Polytoria/Docs-v2
- lua-definitions: `af1a720fecbe` (2026-02-26) from https://github.com/Polytoria/lua-definitions

## Summary

| Metric | Count |
| --- | ---: |
| Official API types | 157 |
| Official API enums | 36 |
| Official globals | 45 |
| VRS catalog nodes | 874 |
| API types with any VRS coverage | 126 |
| API types without VRS coverage | 31 |
| Gameplay API types | 138 |
| Gameplay API types covered | 126 |
| Creator API types | 19 |
| Creator API types covered | 0 |
| VRS target runtime API types | 119 |
| VRS target runtime types covered | 119 |
| Low-confidence / inferred catalog nodes | 0 |
| Catalog nodes without API metadata | 0 |

## Coverage Percentages

| Metric | Percentage | Fraction |
| --- | ---: | ---: |
| API types with any VRS coverage | 80.25% | 126/157 |
| API types without VRS coverage | 19.75% | 31/157 |
| Direct API type coverage | 57.96% | 91/157 |
| Partial API type coverage | 21.02% | 33/157 |
| Indirect or synthetic API type coverage | 1.27% | 2/157 |
| Inferred API type coverage | 0% | 0/157 |
| Gameplay API coverage | 91.3% | 126/138 |
| Gameplay API still uncovered | 8.7% | 12/138 |
| Creator API coverage | 0% | 0/19 |
| Creator API still uncovered | 100% | 19/19 |
| VRS target runtime coverage | 100% | 119/119 |
| VRS target runtime still uncovered | 0% | 0/119 |
| Low-confidence / inferred catalog nodes | 0% | 0/874 |
| Catalog nodes without API metadata | 0% | 0/874 |

The official API percentage is type-level coverage, not member-level coverage. `Gameplay API` means runtime/player-facing APIs, including game UI. `Creator API` means editor, addon, tooling, or non-gameplay infrastructure APIs.

## Catalog Nodes By Kind

| Kind | Count |
| --- | ---: |
| Action | 285 |
| Condition | 123 |
| Property | 355 |
| Trigger | 111 |

## Type Coverage

| Coverage | Types |
| --- | ---: |
| Direct | 91 |
| Partial | 33 |
| Indirect or synthetic | 2 |
| Inferred from `apiType` | 0 |
| Uncovered | 31 |

## Coverage By API Surface

| Surface | Official types | Covered | Uncovered | Coverage |
| --- | ---: | ---: | ---: | ---: |
| Creator | 19 | 0 | 19 | 0% |
| Gameplay | 138 | 126 | 12 | 91.3% |

## Coverage By API Family

| Family | Official types | Covered | Uncovered | VRS target |
| --- | ---: | ---: | ---: | ---: |
| Assets | 17 | 17 | 0 | 17 |
| Creator/Addons | 10 | 0 | 10 | 0 |
| Data/Stats | 13 | 13 | 0 | 13 |
| Infrastructure | 26 | 5 | 21 | 0 |
| Input/Network | 11 | 11 | 0 | 9 |
| Math/Structs | 8 | 8 | 0 | 8 |
| Runtime Gameplay | 54 | 54 | 0 | 54 |
| UI | 18 | 18 | 0 | 18 |

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

## VRS Node Coverage Roadmap

No uncovered target-runtime API types are currently prioritized.

## Gameplay Infrastructure Not Prioritized

These official Gameplay-surface types are intentionally excluded from the normal VRS target-runtime score. They are global services, containers, platform features, or advanced integration points rather than core artist-facing node graph building blocks.

Do not chase these rows just to raise the broad Gameplay percentage. Add them only when there is a concrete workflow, safe runtime behavior, and a clear non-scripter UX.

| API type | Coverage | Why it is not in the priority score |
| --- | --- | --- |
| `AchievementsService` | Uncovered | Useful later, but needs an achievement setup workflow before artist-facing nodes are safe. |
| `AssetsService` | Uncovered | Asset object nodes are covered; service lookup nodes need a clearer file-link workflow. |
| `CaptureService` | Uncovered | Screenshot/photo capture is a specialized platform feature, not core gameplay logic. |
| `HttpService` | Uncovered | Network requests are advanced and can be unsafe/noisy for a beginner palette without allowlists and error handling UX. |
| `InsertService` | Uncovered | Runtime insertion can be useful, but needs placement, ownership, and failure handling UX first. |
| `PresenceService` | Uncovered | Client presence is useful later, but it is not core rule/action gameplay behavior. |
| `PurchasesService` | Uncovered | Monetization nodes need careful UX, testing, and guardrails before being exposed. |
| `ScriptService` | Uncovered | Script storage is handled by VRS deploy/link workflows, not normal gameplay nodes. |
| `ServerHidden` | Uncovered | Server-side hidden containers are useful infrastructure, but current VRS input events use World/Hidden; direct wrapper nodes need a concrete workflow first. |
| `SocialService` | Uncovered | Documented as WIP or platform-level flow; keep out until a concrete safe workflow exists. |
| `Temporary` | Uncovered | Server-side hidden containers are useful infrastructure, but current VRS input events use World/Hidden; direct wrapper nodes need a concrete workflow first. |
| `WorldsService` | Uncovered | Documented as WIP or platform-level flow; keep out until a concrete safe workflow exists. |

## Creator / Non-Gameplay APIs

These official types are tracked for transparency, but they are not part of the normal user-node roadmap unless a future Creator/tooling workflow needs them.

| API type | Family | Coverage | Confidence |
| --- | --- | --- | --- |
| `AddonObject` | Creator/Addons | Uncovered | None |
| `AddonToolItem` | Creator/Addons | Uncovered | None |
| `CreatorAddons` | Creator/Addons | Uncovered | None |
| `CreatorContextService` | Creator/Addons | Uncovered | None |
| `CreatorGUI` | Creator/Addons | Uncovered | None |
| `CreatorHistory` | Creator/Addons | Uncovered | None |
| `CreatorInterface` | Creator/Addons | Uncovered | None |
| `CreatorSelections` | Creator/Addons | Uncovered | None |
| `CreatorService` | Creator/Addons | Uncovered | None |
| `PreferencesService` | Creator/Addons | Uncovered | None |
| `FilterService` | Infrastructure | Uncovered | None |
| `HttpRequestData` | Infrastructure | Uncovered | None |
| `HttpResponseData` | Infrastructure | Uncovered | None |
| `IOService` | Infrastructure | Uncovered | None |
| `NewServerRequestData` | Infrastructure | Uncovered | None |
| `PTCallback` | Infrastructure | Uncovered | None |
| `PTFunction` | Infrastructure | Uncovered | None |
| `PTSignal` | Infrastructure | Uncovered | None |
| `PTSignalConnection` | Infrastructure | Uncovered | None |

## Covered Types

| API type | Surface | Category | Coverage | Confidence | VRS nodes |
| --- | --- | --- | --- | --- | --- |
| `Accessory` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetAccessoryAttachment (Set Accessory Attachment)<br>PROP_AccessoryAttachment (Accessory Attachment) |
| `Animator` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_PlayCharacterAnimation (Play Character Animation)<br>ACT_PlayCharacterOneShotAnimation (Play Character One Shot)<br>ACT_StopCharacterAnimation (Stop Character Animation)<br>ACT_StopCharacterOneShotAnimation (Stop Character One Shot)<br>PROP_CharacterAnimator (Character Animation Controller)<br>PROP_CurrentCharacterAnimation (Current Character Animation) |
| `AudioAsset` | Gameplay | Assets | Partial | Explicit | ACT_SetBuiltInAudioPreset (Set Built-In Audio Preset)<br>ACT_SetPTAudioAssetId (Set PT Audio Asset ID)<br>ACT_SetSoundAudio (Set Sound Audio)<br>PROP_BuiltInAudioPreset (Built-In Audio Preset)<br>PROP_PTAudioAssetId (PT Audio Asset ID)<br>PROP_SoundAudio (Sound Audio) |
| `BaseAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetBuiltInAudioPreset (Set Built-In Audio Preset)<br>ACT_SetBuiltInFontSettings (Set Built-In Font Settings)<br>ACT_SetFileLinkAssetId (Set File Link Asset ID)<br>ACT_SetGradientImageSize (Set Gradient Image Size)<br>ACT_SetMeshAnimationType (Set Mesh Animation Type)<br>ACT_SetPTAudioAssetId (Set PT Audio Asset ID)<br>ACT_SetPTImageAssetId (Set PT Image Asset ID)<br>ACT_SetPTMeshAnimationAssetId (Set PT Mesh Animation Asset ID)<br>... 4 more |
| `BindableEvent` | Gameplay | Input/Network | Direct | Explicit | ACT_FireBindableEvent (Fire Bindable Event)<br>EV_OnBindableEvent (On Bindable Event)<br>PROP_TriggeringBindablePayload (Bindable Event Payload Text) |
| `BodyPosition` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetBodyPositionAcceptanceDistance (Set Body Position Stop Distance)<br>ACT_SetBodyPositionForce (Set Body Position Force)<br>ACT_SetBodyPositionTarget (Set Body Position Target)<br>COND_BodyPositionForceAtLeast (Body Position Force At Least)<br>COND_BodyPositionReachedTarget (Body Position Reached Target)<br>EV_OnBodyPositionReachedTarget (On Body Position Reached Target)<br>PROP_BodyPositionAcceptanceDistance (Body Position Stop Distance)<br>PROP_BodyPositionDistanceToTarget (Body Position Distance To Target)<br>... 2 more |
| `BoolValue` | Gameplay | Data/Stats | Direct | Mixed | ACT_SetState (Set State)<br>ACT_SetValueObjectValue (Set Value Object)<br>ACT_ToggleState (Toggle State)<br>COND_StateIsFalse (State Is False)<br>COND_StateIsTrue (State Is True)<br>EV_OnStateChanged (On State Changed)<br>PROP_ValueObjectValue (Value Object) |
| `Bounds` | Gameplay | Math/Structs | Direct | Explicit | COND_ObjectBoundsContainsPoint (Object Bounds Contains Point)<br>PROP_ObjectBoundsCenter (Object Bounds Center)<br>PROP_ObjectBoundsExtents (Object Bounds Extents)<br>PROP_ObjectBoundsSize (Object Bounds Size)<br>PROP_ObjectBoundsVolume (Object Bounds Volume) |
| `BuiltInAudioAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetBuiltInAudioPreset (Set Built-In Audio Preset)<br>PROP_BuiltInAudioPreset (Built-In Audio Preset) |
| `BuiltInFontAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetBuiltInFontSettings (Set Built-In Font Settings)<br>PROP_BuiltInFontPreset (Built-In Font Preset) |
| `Camera` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetCameraFOV (Set Camera FOV)<br>PROP_CameraFOV (Camera FOV)<br>PROP_CurrentCamera (Current Camera) |
| `CharacterModel` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_PlayCharacterAnimation (Play Character Animation)<br>ACT_PlayCharacterOneShotAnimation (Play Character One Shot)<br>ACT_SetCharacterAnimationSpeed (Set Character Animation Speed)<br>ACT_SetCharacterState (Set Character State)<br>ACT_StopCharacterAnimation (Stop Character Animation)<br>ACT_StopCharacterOneShotAnimation (Stop Character One Shot)<br>PROP_CharacterAnimationSpeed (Character Animation Speed)<br>PROP_CharacterAnimator (Character Animation Controller)<br>... 3 more |
| `ChatService` | Gameplay | Infrastructure | Partial | AutoVerified | ACT_BroadcastChatMessage (Broadcast Chat Message)<br>ACT_SendChatMessageToPlayer (Send Chat Message To Player)<br>EV_OnChatMessage (On Chat Message) |
| `ClientScript` | Gameplay | Runtime Gameplay | Partial | Explicit | ACT_CallScriptFunction (Call Script Function)<br>ACT_CallScriptFunctionAsync (Call Script Function Async)<br>ACT_DisableScript (Disable Script)<br>ACT_EnableScript (Enable Script)<br>ACT_SetScriptEnabled (Set Script Enabled)<br>ACT_ToggleScriptEnabled (Toggle Script Enabled)<br>COND_ScriptIsEnabled (Script Is Enabled)<br>PROP_ScriptEnabled (Script Enabled) |
| `Clothing` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetClothingImage (Set Clothing Image)<br>PROP_ClothingImage (Clothing Image) |
| `Color` | Gameplay | Math/Structs | Partial | Mixed | ACT_SetCharacterBodyColor (Set Character Body Color)<br>ACT_SetEntityColor (Set Entity Color)<br>ACT_SetObjectColor (Set Object Color)<br>PROP_AmbientColor (Ambient Color)<br>PROP_CharacterBodyColor (Character Body Color)<br>PROP_EntityColor (Entity Color)<br>PROP_LightColor (Light Color)<br>PROP_ObjectColor (Object Color)<br>... 2 more |
| `ColorAdjustModifier` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetColorAdjustBrightness (Set Color Adjust Brightness)<br>ACT_SetColorAdjustContrast (Set Color Adjust Contrast)<br>ACT_SetColorAdjustSaturation (Set Color Adjust Saturation)<br>ACT_SetColorAdjustTint (Set Color Adjust Tint)<br>PROP_ColorAdjustBrightness (Color Adjust Brightness)<br>PROP_ColorAdjustContrast (Color Adjust Contrast)<br>PROP_ColorAdjustSaturation (Color Adjust Saturation)<br>PROP_ColorAdjustTint (Color Adjust Tint) |
| `ColorSeries` | Gameplay | Math/Structs | Direct | Explicit | PROP_ColorFromColorSeries (Color From Color Series)<br>PROP_ColorSeriesFromColors (Color Series From Colors)<br>PROP_ColorSeriesPointColor (Color Series Point Color)<br>PROP_ColorSeriesPointCount (Color Series Point Count)<br>PROP_ColorSeriesPointOffset (Color Series Point Offset) |
| `ColorValue` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetValueObjectValue (Set Value Object)<br>PROP_ValueObjectValue (Value Object) |
| `CoreUIService` | Gameplay | UI | Direct | Explicit | ACT_SetBuiltInUIVisible (Set Built-In UI Visible)<br>PROP_BuiltInBackpackAvailable (Backpack Is Available)<br>PROP_BuiltInChatVisible (Chat Is Visible)<br>PROP_BuiltInEmoteWheelVisible (Emote Wheel Is Visible)<br>PROP_BuiltInHealthBarVisible (Health Bar Is Visible)<br>PROP_BuiltInHotbarVisible (Hotbar Is Visible)<br>PROP_BuiltInLeaderboardVisible (Leaderboard Is Visible)<br>PROP_BuiltInMenuButtonVisible (Menu Button Is Visible)<br>... 2 more |
| `Datastore` | Gameplay | Data/Stats | Direct | Explicit | ACT_RemoveDatastoreValue (Remove Saved Value)<br>ACT_SaveDatastoreValue (Save Saved Value)<br>PROP_DatastoreKey (Saved Store Name)<br>PROP_DatastoreValue (Saved Value) |
| `DatastoreService` | Gameplay | Data/Stats | Direct | Explicit | ACT_RemoveDatastoreValue (Remove Saved Value)<br>ACT_SaveDatastoreValue (Save Saved Value)<br>PROP_DatastoreKey (Saved Store Name)<br>PROP_DatastoreValue (Saved Value) |
| `Decal` | Gameplay | Assets | Direct | Explicit | ACT_SetDecalImage (Set Decal Image)<br>PROP_DecalImage (Decal Image) |
| `Dynamic` | Gameplay | Runtime Gameplay | Direct | Explicit | COND_ObjectBoundsContainsPoint (Object Bounds Contains Point)<br>PROP_CharacterAttachment (Character Attachment)<br>PROP_ObjectBoundsCenter (Object Bounds Center)<br>PROP_ObjectBoundsExtents (Object Bounds Extents)<br>PROP_ObjectBoundsSize (Object Bounds Size)<br>PROP_ObjectBoundsVolume (Object Bounds Volume) |
| `Entity` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetEntityCastsShadows (Set Entity Shadows)<br>ACT_SetEntityColor (Set Entity Color)<br>ACT_SetEntityIsSpawn (Set Entity Spawn)<br>PROP_EntityCastsShadows (Entity Shadows)<br>PROP_EntityColor (Entity Color)<br>PROP_EntityIsSpawn (Entity Is Spawn) |
| `Environment` | Gameplay | Infrastructure | Direct | Explicit | ACT_RebuildNavMesh (Rebuild Nav Mesh)<br>ACT_SetAutoGenerateNavMesh (Set Auto Nav Mesh)<br>ACT_SetPartDestroyHeight (Set Fall Destroy Height)<br>ACT_SetWorldGravity (Set World Gravity)<br>COND_RaycastHits (Raycast Hits)<br>PROP_AutoGenerateNavMesh (Auto Nav Mesh)<br>PROP_CurrentCamera (Current Camera)<br>PROP_PartDestroyHeight (Fall Destroy Height)<br>... 6 more |
| `Explosion` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetExplosionAffectAnchored (Set Explosion Affect Anchored)<br>ACT_SetExplosionDamage (Set Explosion Damage)<br>ACT_SetExplosionForce (Set Explosion Force)<br>ACT_SetExplosionRadius (Set Explosion Radius)<br>EV_OnExplosionTouched (On Explosion Touched Object)<br>PROP_ExplosionAffectAnchored (Explosion Affect Anchored)<br>PROP_ExplosionDamage (Explosion Damage)<br>PROP_ExplosionForce (Explosion Force)<br>... 1 more |
| `FileLinkAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetFileLinkAssetId (Set File Link Asset ID)<br>PROP_FileLinkAssetId (File Link Asset ID) |
| `Folder` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_CreateSceneContainer (Create Scene Container) |
| `FontAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetBuiltInFontSettings (Set Built-In Font Settings)<br>PROP_BuiltInFontPreset (Built-In Font Preset)<br>PROP_FontAssetReference (Font Asset Reference) |
| `Grabbable` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetGrabForce (Set Grab Force)<br>ACT_SetGrabMaxRange (Set Grab Drag Range)<br>ACT_SetGrabPermissionMode (Set Grab Permission)<br>ACT_SetGrabPickupRange (Set Grab Pickup Range)<br>ACT_SetGrabUsesDragForce (Set Grab Uses Drag Force)<br>EV_OnObjectGrabbed (On Object Grabbed)<br>EV_OnObjectReleased (On Object Released)<br>PROP_CurrentGrabber (Current Grabber)<br>... 5 more |
| `GradientImageAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetGradientImageSize (Set Gradient Image Size)<br>PROP_GradientImageWidth (Gradient Image Width) |
| `GradientSky` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetGradientSkyColors (Set Gradient Sky Colors)<br>ACT_SetGradientSkyHorizonLine (Set Gradient Sky Horizon Line)<br>ACT_SetGradientSkySunDisc (Set Gradient Sky Sun Disc)<br>ACT_SetGradientSkySunHalo (Set Gradient Sky Sun Halo)<br>PROP_GradientSkyBottomColor (Gradient Sky Bottom Color)<br>PROP_GradientSkyExponent (Gradient Sky Exponent)<br>PROP_GradientSkyHorizonLineColor (Gradient Sky Horizon Line Color)<br>PROP_GradientSkyHorizonLineContribution (Gradient Sky Horizon Line Contribution)<br>... 8 more |
| `GUI` | Gameplay | UI | Direct | Explicit | ACT_SetUIVisible (Set UI Visible)<br>PROP_UIVisible (UI Visible) |
| `GUI3D` | Gameplay | UI | Direct | Explicit | ACT_SetGui3DFaceCamera (Set 3D UI Face Camera)<br>ACT_SetGui3DShaded (Set 3D UI Shaded)<br>ACT_SetGui3DTransparent (Set 3D UI Transparent)<br>PROP_Gui3DFaceCamera (3D UI Faces Camera)<br>PROP_Gui3DShaded (3D UI Shaded)<br>PROP_Gui3DTransparent (3D UI Transparent) |
| `Hidden` | Gameplay | Infrastructure | Indirect | Explicit | ACT_SendInputEvent (Send Input Event)<br>ACT_SendInputTextEvent (Send Input Text Event)<br>EV_OnVrsInputEvent (On Input Network Event) |
| `HiddenBase` | Gameplay | Infrastructure | Indirect | Explicit | ACT_SendInputEvent (Send Input Event)<br>ACT_SendInputTextEvent (Send Input Text Event)<br>EV_OnVrsInputEvent (On Input Network Event) |
| `Image3D` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_Set3DImageColor (Set 3D Image Color)<br>ACT_Set3DImageFaceCamera (Set 3D Image Face Camera)<br>ACT_Set3DImageLighting (Set 3D Image Lighting)<br>ACT_Set3DImageShadows (Set 3D Image Shadows)<br>ACT_Set3DImageTextureOffset (Set 3D Image Texture Offset)<br>ACT_Set3DImageTextureScale (Set 3D Image Texture Scale)<br>COND_3DImageCastsShadows (3D Image Casts Shadows)<br>COND_3DImageFacesCamera (3D Image Faces Camera)<br>... 5 more |
| `ImageAsset` | Gameplay | Assets | Partial | Explicit | ACT_SetCharacterFaceImage (Set Character Face Image)<br>ACT_SetClothingImage (Set Clothing Image)<br>ACT_SetDecalImage (Set Decal Image)<br>ACT_SetGradientImageSize (Set Gradient Image Size)<br>ACT_SetImageSkyAllImages (Set Image Sky Images)<br>ACT_SetImageSkyBackImage (Set Image Sky Back Image)<br>ACT_SetImageSkyBottomImage (Set Image Sky Bottom Image)<br>ACT_SetImageSkyFrontImage (Set Image Sky Front Image)<br>... 17 more |
| `ImageSky` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetImageSkyAllImages (Set Image Sky Images)<br>ACT_SetImageSkyBackImage (Set Image Sky Back Image)<br>ACT_SetImageSkyBottomImage (Set Image Sky Bottom Image)<br>ACT_SetImageSkyFrontImage (Set Image Sky Front Image)<br>ACT_SetImageSkyLeftImage (Set Image Sky Left Image)<br>ACT_SetImageSkyRightImage (Set Image Sky Right Image)<br>ACT_SetImageSkyTopImage (Set Image Sky Top Image)<br>PROP_ImageSkyBackImage (Image Sky Back Image)<br>... 5 more |
| `InputAction` | Gameplay | Input/Network | Partial | Explicit | ACT_BindInputButtonKey (Bind Input Button Key)<br>COND_InputActionExists (Input Action Exists) |
| `InputActionAxis` | Gameplay | Input/Network | Direct | Explicit | PROP_InputAxisValue (Input Axis Value) |
| `InputActionButton` | Gameplay | Input/Network | Direct | Explicit | ACT_BindInputButtonKey (Bind Input Button Key)<br>COND_InputButtonDown (Input Button Down)<br>EV_OnInputButtonDown (On Input Button Down) |
| `InputActionVector2` | Gameplay | Input/Network | Partial | Explicit | PROP_InputVectorX (Input Vector X)<br>PROP_InputVectorY (Input Vector Y) |
| `InputButton` | Gameplay | Input/Network | Direct | Explicit | ACT_BindInputButtonKey (Bind Input Button Key)<br>PROP_InputButtonFromKey (Input Button From Key) |
| `InputButtonCollection` | Gameplay | Input/Network | Direct | Explicit | ACT_BindInputButtonKey (Bind Input Button Key) |
| `InputService` | Gameplay | Input/Network | Direct | Explicit | ACT_BindInputButtonKey (Bind Input Button Key)<br>COND_InputActionExists (Input Action Exists)<br>COND_InputButtonDown (Input Button Down)<br>EV_OnInputButtonDown (On Input Button Down)<br>PROP_InputAxisValue (Input Axis Value)<br>PROP_InputVectorX (Input Vector X)<br>PROP_InputVectorY (Input Vector Y) |
| `Instance` | Gameplay | Runtime Gameplay | Direct | Mixed | ACT_CloneObject (Clone Object)<br>ACT_CreateSceneContainer (Create Scene Container)<br>ACT_CreateUIContainer (Create UI Container)<br>ACT_DestroyObject (Destroy Object)<br>ACT_GiveToolToPlayer (Give Tool To Player)<br>ACT_SetInstanceValueObject (Set Stored Object Reference)<br>ACT_SetObjectName (Set Object Name)<br>ACT_SetObjectParent (Set Object Parent)<br>... 21 more |
| `InstanceValue` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetInstanceValueObject (Set Stored Object Reference)<br>PROP_InstanceValueObject (Stored Object Reference) |
| `IntValue` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetIntegerValueObject (Set Integer Value Object)<br>PROP_IntegerValueObject (Integer Value Object) |
| `Inventory` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_GiveToolToPlayer (Give Tool To Player)<br>COND_PlayerHasTool (Player Has Tool)<br>PROP_FindToolInInventory (Find Tool In Inventory)<br>PROP_PlayerInventory (Player Inventory) |
| `Light` | Gameplay | Runtime Gameplay | Direct | Mixed | ACT_SetLightBrightness (Set Light Brightness)<br>ACT_SetLightColor (Set Light Color)<br>ACT_SetLightShadows (Set Light Shadows)<br>ACT_SetLightShine (Set Light Shine)<br>ACT_SetSunLightBrightness (Set Sun Light Brightness)<br>ACT_SetSunLightColor (Set Sun Light Color)<br>ACT_SetSunLightShadows (Set Sun Light Shadows)<br>ACT_SetSunLightShine (Set Sun Light Shine)<br>... 4 more |
| `Lighting` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetAmbientColor (Set Ambient Color)<br>ACT_SetFogColor (Set Fog Color)<br>ACT_SetFogDistances (Set Fog Distances)<br>ACT_SetFogEnabled (Set Fog Enabled) |
| `LightingModifier` | Gameplay | Runtime Gameplay | Partial | Explicit | ACT_SetColorAdjustBrightness (Set Color Adjust Brightness)<br>ACT_SetColorAdjustContrast (Set Color Adjust Contrast)<br>ACT_SetColorAdjustSaturation (Set Color Adjust Saturation)<br>ACT_SetColorAdjustTint (Set Color Adjust Tint)<br>PROP_ColorAdjustBrightness (Color Adjust Brightness)<br>PROP_ColorAdjustContrast (Color Adjust Contrast)<br>PROP_ColorAdjustSaturation (Color Adjust Saturation)<br>PROP_ColorAdjustTint (Color Adjust Tint) |
| `Marker3D` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetMarkerAppearsOnTop (Set Marker On Top)<br>ACT_SetMarkerLength (Set Marker Length)<br>ACT_SetMarkerVisibleInDev (Set Marker Visible In Dev)<br>PROP_MarkerAppearsOnTop (Marker On Top)<br>PROP_MarkerLength (Marker Length)<br>PROP_MarkerVisibleInDev (Marker Visible In Dev) |
| `Mesh` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_PlayMeshAnimation (Play Mesh Animation)<br>ACT_StopMeshAnimation (Stop Mesh Animation)<br>EV_OnMeshLoaded (On Mesh Loaded) |
| `MeshAnimationAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetMeshAnimationType (Set Mesh Animation Type)<br>ACT_SetPTMeshAnimationAssetId (Set PT Mesh Animation Asset ID)<br>PROP_PTMeshAnimationAssetId (PT Mesh Animation Asset ID) |
| `MeshAnimationInfo` | Gameplay | Assets | Direct | Explicit | PROP_MeshAnimationInfoName (Mesh Animation Info Name) |
| `MeshAsset` | Gameplay | Assets | Partial | Explicit | ACT_SetCharacterBodyMesh (Set Character Body Mesh)<br>ACT_SetPTMeshAssetId (Set PT Mesh Asset ID)<br>PROP_CharacterBodyMesh (Character Body Mesh)<br>PROP_PTMeshAssetId (PT Mesh Asset ID) |
| `MissingInstance` | Gameplay | Runtime Gameplay | Direct | Explicit | COND_ObjectIsMissingInstance (Object Is Missing Instance)<br>PROP_ObjectIsMissingInstance (Object Is Missing Instance) |
| `Model` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_CreateSceneContainer (Create Scene Container) |
| `ModuleScript` | Gameplay | Runtime Gameplay | Partial | Explicit | ACT_CallScriptFunction (Call Script Function)<br>ACT_CallScriptFunctionAsync (Call Script Function Async)<br>ACT_DisableScript (Disable Script)<br>ACT_EnableScript (Enable Script)<br>ACT_SetScriptEnabled (Set Script Enabled)<br>ACT_ToggleScriptEnabled (Toggle Script Enabled)<br>COND_ScriptIsEnabled (Script Is Enabled)<br>PROP_ScriptEnabled (Script Enabled) |
| `NetMessage` | Gameplay | Input/Network | Direct | Explicit | ACT_SendInputTextEvent (Send Input Text Event)<br>PROP_TriggeringInputText (Input Event Text) |
| `NetworkedObject` | Gameplay | Input/Network | Direct | Explicit | PROP_ObjectIsNetworked (Object Is Networked)<br>PROP_ObjectNetworkKey (Object Network Key)<br>PROP_ObjectSaveKey (Object Save Key)<br>PROP_ObjectTypeName (Object Type Name) |
| `NetworkEvent` | Gameplay | Input/Network | Direct | Explicit | ACT_SendInputEvent (Send Input Event)<br>ACT_SendInputTextEvent (Send Input Text Event)<br>EV_OnVrsInputEvent (On Input Network Event) |
| `NPC` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_DamageNPC (Damage NPC)<br>ACT_HealNPC (Heal NPC)<br>ACT_KillNPC (Kill NPC)<br>ACT_MakeNPCJump (Make NPC Jump)<br>ACT_SetNPCHealth (Set NPC Health)<br>ACT_SetNPCJumpPower (Set NPC Jump Power)<br>ACT_SetNPCNavigationTarget (Set NPC Navigation Target)<br>ACT_SetNPCWalkSpeed (Set NPC Walk Speed)<br>... 13 more |
| `NumberRange` | Gameplay | Math/Structs | Partial | AutoVerified | COND_NumberCompare (Number Compare) |
| `NumberValue` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetValueObjectValue (Set Value Object)<br>PROP_ValueObjectValue (Value Object) |
| `Part` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_HideObject (Hide Object)<br>ACT_MoveObjectDown (Move Object Down)<br>ACT_MoveObjectToAnotherObject (Move Object To Another Object)<br>ACT_MoveObjectUp (Move Object Up)<br>ACT_SetObjectAnchored (Set Object Anchored)<br>ACT_SetObjectCanCollide (Set Object Can Collide)<br>ACT_SetObjectDepthSize (Set Object Depth Size)<br>ACT_SetObjectHeightPosition (Set Object Height Position)<br>... 55 more |
| `Particles` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_BurstParticles (Burst Particles)<br>ACT_SetParticleAmount (Set Particle Amount)<br>ACT_StartParticles (Start Particles)<br>ACT_StopParticles (Stop Particles) |
| `Physical` | Gameplay | Runtime Gameplay | Direct | Mixed | ACT_MoveObjectWithPhysics (Move Object With Physics)<br>ACT_SetObjectSpinVelocity (Set Object Spin Velocity)<br>ACT_SetObjectVelocity (Set Object Velocity)<br>ACT_StartMovingPlatformLoop (Start Moving Platform Loop)<br>ACT_TurnObjectWithPhysics (Turn Object With Physics)<br>COND_ObjectIsMoving (Object Is Moving)<br>COND_ObjectSpeedAtLeast (Object Speed Is At Least)<br>EV_OnCheckpointTouched (On Checkpoint Touched)<br>... 10 more |
| `Player` | Gameplay | Runtime Gameplay | Direct | Mixed | ACT_GiveToolToPlayer (Give Tool To Player)<br>ACT_KillPlayer (Kill Player)<br>ACT_MakePlayerJump (Make Player Jump)<br>ACT_RespawnPlayer (Respawn Player)<br>ACT_SetPlayerCanMove (Set Player Can Move)<br>ACT_SetPlayerGameTeam (Set Player Game Team)<br>COND_PlayerHasTool (Player Has Tool)<br>COND_PlayerIsInGameTeam (Player Is In Game Team)<br>... 10 more |
| `PlayerDefaults` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetJumpPower (Set JumpPower)<br>ACT_SetMaxHealth (Set MaxHealth)<br>ACT_SetRespawnTime (Set RespawnTime)<br>ACT_SetSprintSpeed (Set SprintSpeed)<br>ACT_SetStamina (Set Stamina)<br>ACT_SetWalkSpeed (Set WalkSpeed) |
| `PlayerGUI` | Gameplay | UI | Direct | Explicit | PROP_PlayerUIRoot (Player UI Root) |
| `Players` | Gameplay | Runtime Gameplay | Direct | Mixed | COND_PlayerCountAtLeast (Player Count At Least)<br>COND_PlayerCountAtMost (Player Count At Most)<br>COND_PlayerExists (Player Exists)<br>EV_OnEnoughPlayers (On Enough Players)<br>EV_OnNotEnoughPlayers (On Not Enough Players)<br>EV_OnPlayerCountDroppedTo (On Player Count Dropped To)<br>EV_OnPlayerCountReached (On Player Count Reached)<br>EV_OnPlayerJoined (On Player Joined)<br>... 1 more |
| `PointLight` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetPointLightRange (Set Point Light Range) |
| `PolytorianModel` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_ClearCharacterAppearance (Clear Character Appearance)<br>ACT_LoadCharacterAppearance (Load Character Appearance)<br>ACT_SetCharacterBodyColor (Set Character Body Color)<br>ACT_SetCharacterBodyMesh (Set Character Body Mesh)<br>ACT_SetCharacterFaceImage (Set Character Face Image)<br>ACT_StartCharacterRagdoll (Start Character Ragdoll)<br>ACT_StopCharacterRagdoll (Stop Character Ragdoll)<br>EV_OnCharacterRagdollStarted (On Character Ragdoll Started)<br>... 7 more |
| `ProceduralSky` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetProceduralSkyExposure (Set Procedural Sky Exposure)<br>ACT_SetProceduralSkyGroundColor (Set Procedural Sky Ground Color)<br>ACT_SetProceduralSkyHorizonColor (Set Procedural Sky Horizon Color)<br>ACT_SetProceduralSkySunSize (Set Procedural Sky Sun Size)<br>ACT_SetProceduralSkyTint (Set Procedural Sky Tint)<br>PROP_ProceduralSkyExposure (Procedural Sky Exposure)<br>PROP_ProceduralSkyGroundColor (Procedural Sky Ground Color)<br>PROP_ProceduralSkyHorizonColor (Procedural Sky Horizon Color)<br>... 2 more |
| `PTAudioAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetPTAudioAssetId (Set PT Audio Asset ID)<br>PROP_PTAudioAssetId (PT Audio Asset ID) |
| `PTImageAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetPTImageAssetId (Set PT Image Asset ID)<br>PROP_PTImageAssetId (PT Image Asset ID) |
| `PTMeshAnimationAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetPTMeshAnimationAssetId (Set PT Mesh Animation Asset ID)<br>PROP_PTMeshAnimationAssetId (PT Mesh Animation Asset ID) |
| `PTMeshAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetPTMeshAssetId (Set PT Mesh Asset ID)<br>PROP_PTMeshAssetId (PT Mesh Asset ID) |
| `Quaternion` | Gameplay | Math/Structs | Direct | Explicit | COND_QuaternionAngleAtMost (Rotation Angle At Most)<br>PROP_QuaternionAngle (Rotation Angle Difference)<br>PROP_QuaternionDot (Rotation Dot)<br>PROP_QuaternionFromAxisAngle (Rotation Around Axis)<br>PROP_QuaternionFromComponents (Quaternion From Components)<br>PROP_QuaternionFromEuler (Rotation From Euler)<br>PROP_QuaternionFromToRotation (Rotation From Direction To Direction)<br>PROP_QuaternionIdentity (Identity Rotation)<br>... 7 more |
| `RayResult` | Gameplay | Math/Structs | Direct | Explicit | COND_RaycastHits (Raycast Hits)<br>PROP_RaycastHitDistance (Raycast Hit Distance)<br>PROP_RaycastHitNormal (Raycast Hit Normal)<br>PROP_RaycastHitObject (Raycast Hit Object)<br>PROP_RaycastHitPosition (Raycast Hit Position)<br>PROP_RaycastResult (Raycast Result) |
| `ResourceAsset` | Gameplay | Assets | Direct | Explicit | ACT_SetBuiltInAudioPreset (Set Built-In Audio Preset)<br>ACT_SetBuiltInFontSettings (Set Built-In Font Settings)<br>ACT_SetGradientImageSize (Set Gradient Image Size)<br>ACT_SetMeshAnimationType (Set Mesh Animation Type)<br>ACT_SetPTAudioAssetId (Set PT Audio Asset ID)<br>ACT_SetPTImageAssetId (Set PT Image Asset ID)<br>ACT_SetPTMeshAnimationAssetId (Set PT Mesh Animation Asset ID)<br>ACT_SetPTMeshAssetId (Set PT Mesh Asset ID)<br>... 2 more |
| `RigidBody` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetObjectSpinVelocity (Set Object Spin Velocity)<br>ACT_SetObjectVelocity (Set Object Velocity)<br>ACT_SetRigidBodyAngularDrag (Set Rigid Body Angular Drag)<br>ACT_SetRigidBodyBounciness (Set Rigid Body Bounciness)<br>ACT_SetRigidBodyDrag (Set Rigid Body Drag)<br>ACT_SetRigidBodyFriction (Set Rigid Body Friction)<br>ACT_SetRigidBodyGravity (Set Rigid Body Gravity)<br>ACT_SetRigidBodyMass (Set Rigid Body Mass)<br>... 8 more |
| `Script` | Gameplay | Runtime Gameplay | Direct | Mixed | ACT_CallScriptFunction (Call Script Function)<br>ACT_CallScriptFunctionAsync (Call Script Function Async)<br>ACT_DisableScript (Disable Script)<br>ACT_EnableScript (Enable Script)<br>ACT_RunLuauAction (Run Code Action)<br>ACT_SetScriptEnabled (Set Script Enabled)<br>ACT_ShowMessage (Show Message)<br>ACT_ToggleScriptEnabled (Toggle Script Enabled)<br>... 7 more |
| `ScriptSharedTable` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_AppendSharedText (Append Shared Text)<br>ACT_ClearSharedPrefix (Clear Shared Prefix)<br>ACT_ClearSharedSuffix (Clear Shared Suffix)<br>ACT_ClearSharedValues (Clear Shared Values)<br>ACT_IncrementSharedNumber (Increment Shared Number)<br>ACT_RemoveSharedValue (Remove Shared Value)<br>ACT_SetSharedValue (Set Shared Value)<br>COND_SharedNumberAtLeast (Shared Number At Least)<br>... 4 more |
| `Seat` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetSeatAllowsNPCs (Set Seat Allows NPCs)<br>COND_SeatAllowsNPCs (Seat Allows NPCs)<br>COND_SeatIsOccupied (Seat Is Occupied)<br>EV_OnSeatSat (On Seat Sat)<br>EV_OnSeatVacated (On Seat Left)<br>PROP_SeatAllowsNPCs (Seat Allows NPCs)<br>PROP_SeatOccupant (Seat Occupant) |
| `ServerScript` | Gameplay | Runtime Gameplay | Partial | Explicit | ACT_CallScriptFunction (Call Script Function)<br>ACT_CallScriptFunctionAsync (Call Script Function Async)<br>ACT_DisableScript (Disable Script)<br>ACT_EnableScript (Enable Script)<br>ACT_SetScriptEnabled (Set Script Enabled)<br>ACT_ToggleScriptEnabled (Toggle Script Enabled)<br>COND_ScriptIsEnabled (Script Is Enabled)<br>PROP_ScriptEnabled (Script Enabled) |
| `Sky` | Gameplay | Runtime Gameplay | Partial | Explicit | ACT_SetGradientSkyColors (Set Gradient Sky Colors)<br>ACT_SetGradientSkyHorizonLine (Set Gradient Sky Horizon Line)<br>ACT_SetGradientSkySunDisc (Set Gradient Sky Sun Disc)<br>ACT_SetGradientSkySunHalo (Set Gradient Sky Sun Halo)<br>ACT_SetImageSkyAllImages (Set Image Sky Images)<br>ACT_SetImageSkyBackImage (Set Image Sky Back Image)<br>ACT_SetImageSkyBottomImage (Set Image Sky Bottom Image)<br>ACT_SetImageSkyFrontImage (Set Image Sky Front Image)<br>... 31 more |
| `Sound` | Gameplay | Runtime Gameplay | Direct | Mixed | ACT_PauseSound (Pause Sound)<br>ACT_PlaySound (Play Sound)<br>ACT_PlaySoundOnce (Play Sound Once)<br>ACT_SetSoundAudio (Set Sound Audio)<br>ACT_SetSoundLoop (Set Sound Loop)<br>ACT_SetSoundVolume (Set Sound Volume)<br>ACT_StopSound (Stop Sound)<br>EV_OnSoundLoaded (On Sound Loaded)<br>... 1 more |
| `SpotLight` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetSpotLightAngle (Set Spot Light Angle)<br>ACT_SetSpotLightRange (Set Spot Light Range) |
| `Stat` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetPlayerStatNumber (Set Player Stat Number)<br>ACT_SetPlayerStatText (Set Player Stat Text)<br>COND_PlayerStatAtLeast (Player Stat At Least)<br>PROP_PlayerStatDisplayValue (Player Stat Display Value)<br>PROP_PlayerStatValue (Player Stat Value)<br>PROP_StatDisplayName (Stat Display Name)<br>PROP_TeamStatTotal (Team Stat Total) |
| `Stats` | Gameplay | Data/Stats | Direct | Explicit | PROP_AllPlayerStats (All Player Stats) |
| `StringValue` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetValueObjectValue (Set Value Object)<br>PROP_ValueObjectValue (Value Object) |
| `SunLight` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetSunLightBrightness (Set Sun Light Brightness)<br>ACT_SetSunLightColor (Set Sun Light Color)<br>ACT_SetSunLightShadows (Set Sun Light Shadows)<br>ACT_SetSunLightShine (Set Sun Light Shine)<br>PROP_SunLightBrightness (Sun Light Brightness)<br>PROP_SunLightColor (Sun Light Color)<br>PROP_SunLightShadows (Sun Light Shadows)<br>PROP_SunLightShine (Sun Light Shine) |
| `Team` | Gameplay | Runtime Gameplay | Direct | Explicit | PROP_GameTeamColor (Game Team Color)<br>PROP_GameTeamName (Game Team Name)<br>PROP_GameTeamPlayerCount (Game Team Player Count)<br>PROP_GameTeamPlayers (Game Team Players)<br>PROP_PlayerGameTeamColor (Player Game Team Color)<br>PROP_PlayerGameTeamName (Player Game Team Name) |
| `Teams` | Gameplay | Runtime Gameplay | Direct | Explicit | PROP_AllGameTeams (All Game Teams)<br>PROP_GameTeamCount (Game Team Count) |
| `Text3D` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_Set3DText (Set 3D Text)<br>ACT_Set3DTextColor (Set 3D Text Color)<br>ACT_Set3DTextFaceCamera (Set 3D Text Face Camera)<br>ACT_Set3DTextFontSize (Set 3D Text Size)<br>ACT_Set3DTextLighting (Set 3D Text Lighting)<br>ACT_Set3DTextOutlineColor (Set 3D Text Outline Color)<br>ACT_Set3DTextOutlineWidth (Set 3D Text Outline Width)<br>ACT_Set3DTextRichText (Set 3D Text Rich Text)<br>... 13 more |
| `Tool` | Gameplay | Runtime Gameplay | Partial | Mixed | ACT_ActivateTool (Activate Tool)<br>ACT_DeactivateTool (Deactivate Tool)<br>ACT_GiveToolToPlayer (Give Tool To Player)<br>ACT_PlayToolAnimation (Play Tool Animation)<br>ACT_SetToolDroppable (Set Tool Droppable)<br>COND_PlayerHasTool (Player Has Tool)<br>EV_OnToolActivated (On Tool Activated)<br>EV_OnToolDeactivated (On Tool Deactivated)<br>... 3 more |
| `Truss` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_SetTrussClimbSpeed (Set Truss Climb Speed)<br>PROP_TrussClimbSpeed (Truss Climb Speed) |
| `TweenObject` | Gameplay | Runtime Gameplay | Direct | Explicit | ACT_TweenObjectColor (Animate Object Color)<br>ACT_TweenObjectPosition (Animate Object Position)<br>ACT_TweenObjectRotation (Animate Object Rotation)<br>ACT_TweenObjectScale (Animate Object Scale)<br>ACT_TweenObjectTransparency (Animate Object Transparency) |
| `TweenService` | Gameplay | Infrastructure | Direct | Mixed | ACT_MoveObjectOverTime (Move Object Over Time)<br>ACT_TweenObjectColor (Animate Object Color)<br>ACT_TweenObjectPosition (Animate Object Position)<br>ACT_TweenObjectRotation (Animate Object Rotation)<br>ACT_TweenObjectScale (Animate Object Scale)<br>ACT_TweenObjectTransparency (Animate Object Transparency) |
| `UIButton` | Gameplay | Runtime Gameplay | Partial | AutoVerified | EV_OnUIButtonClicked (On UI Button Clicked) |
| `UIContainer` | Gameplay | UI | Partial | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `UIField` | Gameplay | UI | Direct | Explicit | ACT_SetUIFieldClipDescendants (Set UI Clips Children)<br>ACT_SetUIFieldIgnoresMouse (Set UI Ignores Mouse)<br>ACT_SetUIFieldRotation (Set UI Rotation)<br>ACT_SetUIFieldScale (Set UI Scale)<br>ACT_SetUIFieldZIndex (Set UI Layer)<br>PROP_UIFieldClipDescendants (UI Clips Children)<br>PROP_UIFieldIgnoresMouse (UI Ignores Mouse)<br>PROP_UIFieldRotation (UI Rotation)<br>... 2 more |
| `UIFlowLayout` | Gameplay | UI | Partial | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `UIGridLayout` | Gameplay | UI | Direct | Explicit | ACT_SetGridLayoutColumns (Set Grid Columns)<br>ACT_SetGridLayoutSpacing (Set Grid Spacing)<br>PROP_GridLayoutColumns (Grid Columns)<br>PROP_GridLayoutSpacing (Grid Spacing) |
| `UIHFlow` | Gameplay | UI | Direct | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `UIHLayout` | Gameplay | UI | Direct | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `UIHVLayout` | Gameplay | UI | Direct | Explicit | ACT_SetLayoutChildAlignment (Set Layout Child Alignment)<br>ACT_SetLayoutSpacing (Set Layout Spacing)<br>PROP_LayoutChildAlignment (Layout Child Alignment)<br>PROP_LayoutSpacing (Layout Spacing) |
| `UIImage` | Gameplay | UI | Direct | Explicit | ACT_SetUIImage (Set UI Image)<br>PROP_UIImage (UI Image) |
| `UILabel` | Gameplay | UI | Partial | AutoVerified | ACT_SetUIText (Set UI Text)<br>ACT_SetUITextWrapped (Set UI Text Wrapping)<br>COND_UITextIs (UI Text Is)<br>COND_UITextIsEmpty (UI Text Is Empty)<br>COND_UITextWrapped (UI Text Wraps)<br>PROP_UIFontSize (UI Font Size)<br>PROP_UIText (UI Text)<br>PROP_UITextWrapped (UI Text Wraps) |
| `UIScrollView` | Gameplay | UI | Direct | Explicit | ACT_SetScrollViewMode (Set Scroll View Mode)<br>PROP_ScrollViewHorizontalMode (Horizontal Scroll Mode)<br>PROP_ScrollViewVerticalMode (Vertical Scroll Mode) |
| `UITextInput` | Gameplay | UI | Direct | Explicit | ACT_FocusTextInput (Focus Text Input)<br>ACT_SetTextInputPlaceholder (Set Text Input Placeholder)<br>ACT_SetTextInputReadOnly (Set Text Input Read Only)<br>ACT_SetTextInputText (Set Text Input Text)<br>EV_OnTextInputChanged (On Text Input Changed)<br>EV_OnTextInputSubmitted (On Text Input Submitted)<br>PROP_TextInputPlaceholder (Text Input Placeholder)<br>PROP_TextInputReadOnly (Text Input Read Only)<br>... 1 more |
| `UIVFlow` | Gameplay | UI | Direct | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `UIView` | Gameplay | Runtime Gameplay | Partial | AutoVerified | ACT_SetUIColor (Set UI Color)<br>PROP_UIColor (UI Color) |
| `UIViewport` | Gameplay | UI | Direct | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `UIVLayout` | Gameplay | UI | Direct | Explicit | ACT_CreateUIContainer (Create UI Container) |
| `ValueBase` | Gameplay | Data/Stats | Partial | Mixed | ACT_ClearScriptVariable (Clear Script Variable)<br>ACT_IncrementScriptNumber (Increment Script Number)<br>ACT_SetScriptVariable (Set Script Variable)<br>ACT_SetValueObjectValue (Set Value Object)<br>COND_ScriptVariableExists (Script Variable Exists)<br>COND_ValueEquals (Value Equals)<br>COND_ValueIsEmpty (Value Is Empty)<br>EV_OnScriptVariableChanged (On Script Variable Changed)<br>... 1 more |
| `Vector2` | Gameplay | Math/Structs | Direct | Explicit | COND_Vector2DistanceAtMost (Vector2 Distance At Most)<br>PROP_Vector2Distance (Vector2 Distance)<br>PROP_Vector2FromXY (Vector2 From X Y)<br>PROP_Vector2Lerp (Blend Vector2)<br>PROP_Vector2Magnitude (Vector2 Length)<br>PROP_Vector2Normalized (Normalize Vector2)<br>PROP_Vector2X (Vector2 X)<br>PROP_Vector2Y (Vector2 Y) |
| `Vector2Value` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetValueObjectValue (Set Value Object)<br>PROP_ValueObjectValue (Value Object) |
| `Vector3` | Gameplay | Math/Structs | Partial | Mixed | ACT_AddObjectPosition (Add Object Position)<br>ACT_LookAtPosition (Look At Position)<br>ACT_MoveObject (Move Object)<br>ACT_RotateObject (Rotate Object)<br>ACT_RotateObjectContinuously (Rotate Object Continuously)<br>ACT_SetObjectPosition (Set Object Position)<br>ACT_SetObjectScale (Scale Object)<br>ACT_StartCharacterRagdoll (Start Character Ragdoll)<br>... 17 more |
| `Vector3Value` | Gameplay | Data/Stats | Direct | Explicit | ACT_SetValueObjectValue (Set Value Object)<br>PROP_ValueObjectValue (Value Object) |
| `World` | Gameplay | Runtime Gameplay | Direct | Explicit | PROP_ServerIdentifier (Server Identifier)<br>PROP_ServerTime (Server Time)<br>PROP_WorldIdentifier (World Identifier)<br>PROP_WorldIsLocalTest (Local Test Is Running)<br>PROP_WorldIsOldFormat (Old World Format)<br>PROP_WorldObjectCount (World Object Count)<br>PROP_WorldUptime (World Uptime) |

## Uncovered Types

| API type | Surface | Category | Coverage | Confidence | VRS nodes |
| --- | --- | --- | --- | --- | --- |
| `AchievementsService` | Gameplay | Infrastructure | Uncovered | None |  |
| `AddonObject` | Creator | Creator/Addons | Uncovered | None |  |
| `AddonToolItem` | Creator | Creator/Addons | Uncovered | None |  |
| `AssetsService` | Gameplay | Infrastructure | Uncovered | None |  |
| `CaptureService` | Gameplay | Infrastructure | Uncovered | None |  |
| `CreatorAddons` | Creator | Creator/Addons | Uncovered | None |  |
| `CreatorContextService` | Creator | Creator/Addons | Uncovered | None |  |
| `CreatorGUI` | Creator | Creator/Addons | Uncovered | None |  |
| `CreatorHistory` | Creator | Creator/Addons | Uncovered | None |  |
| `CreatorInterface` | Creator | Creator/Addons | Uncovered | None |  |
| `CreatorSelections` | Creator | Creator/Addons | Uncovered | None |  |
| `CreatorService` | Creator | Creator/Addons | Uncovered | None |  |
| `FilterService` | Creator | Infrastructure | Uncovered | None |  |
| `HttpRequestData` | Creator | Infrastructure | Uncovered | None |  |
| `HttpResponseData` | Creator | Infrastructure | Uncovered | None |  |
| `HttpService` | Gameplay | Infrastructure | Uncovered | None |  |
| `InsertService` | Gameplay | Infrastructure | Uncovered | None |  |
| `IOService` | Creator | Infrastructure | Uncovered | None |  |
| `NewServerRequestData` | Creator | Infrastructure | Uncovered | None |  |
| `PreferencesService` | Creator | Creator/Addons | Uncovered | None |  |
| `PresenceService` | Gameplay | Infrastructure | Uncovered | None |  |
| `PTCallback` | Creator | Infrastructure | Uncovered | None |  |
| `PTFunction` | Creator | Infrastructure | Uncovered | None |  |
| `PTSignal` | Creator | Infrastructure | Uncovered | None |  |
| `PTSignalConnection` | Creator | Infrastructure | Uncovered | None |  |
| `PurchasesService` | Gameplay | Infrastructure | Uncovered | None |  |
| `ScriptService` | Gameplay | Infrastructure | Uncovered | None |  |
| `ServerHidden` | Gameplay | Infrastructure | Uncovered | None |  |
| `SocialService` | Gameplay | Infrastructure | Uncovered | None |  |
| `Temporary` | Gameplay | Infrastructure | Uncovered | None |  |
| `WorldsService` | Gameplay | Infrastructure | Uncovered | None |  |

## Low Confidence / Needs Annotation

No low-confidence nodes were found.

## Notes

- VRS nodes are human workflow nodes. One node can touch several API members, and one API member can require several nodes.
- The report uses GitHub-hosted Polytoria documentation sources because the rendered documentation site may be protected by browser checks.
- Regenerate this file after catalog changes or when Polytoria updates its public API documentation.
