using System.Collections.Generic;
namespace Blitz3D.Converter
{
	public static class Symbols
	{
		public delegate void LinkSymbol(string sym);

		private static void Runtime_link(LinkSymbol linkSymbol)
		{
			linkSymbol("End");
			linkSymbol("Stop");
			linkSymbol("AppTitle$title$close_prompt=\"\"");
			linkSymbol("RuntimeError$message");
			linkSymbol("ExecFile$command");
			linkSymbol("Delay%millisecs");
			linkSymbol("%MilliSecs");
			linkSymbol("$CommandLine");
			linkSymbol("$SystemProperty$property");
			linkSymbol("$GetEnv$env_var");
			linkSymbol("SetEnv$env_var$value");

			linkSymbol("%CreateTimer%hertz");
			linkSymbol("%WaitTimer%timer");
			linkSymbol("FreeTimer%timer");
			linkSymbol("DebugLog$text");

			linkSymbol("_bbDebugStmt");
			linkSymbol("_bbDebugEnter");
			linkSymbol("_bbDebugLeave");
		}

		private static void Basic_link(LinkSymbol linkSymbol)
		{
			linkSymbol("_bbIntType");
			linkSymbol("_bbFltType");
			linkSymbol("_bbStrType");
			linkSymbol("_bbCStrType");

			linkSymbol("_bbStrLoad");
			linkSymbol("_bbStrRelease");
			linkSymbol("_bbStrStore");
			linkSymbol("_bbStrCompare");
			linkSymbol("_bbStrConcat");
			linkSymbol("_bbStrToInt");
			linkSymbol("_bbStrFromInt");
			linkSymbol("_bbStrToFloat");
			linkSymbol("_bbStrFromFloat");
			linkSymbol("_bbStrConst");
			linkSymbol("_bbDimArray");
			linkSymbol("_bbUndimArray");
			linkSymbol("_bbArrayBoundsEx");
			linkSymbol("_bbVecAlloc");
			linkSymbol("_bbVecFree");
			linkSymbol("_bbVecBoundsEx");
			linkSymbol("_bbObjNew");
			linkSymbol("_bbObjDelete");
			linkSymbol("_bbObjDeleteEach");
			linkSymbol("_bbObjRelease");
			linkSymbol("_bbObjStore");
			linkSymbol("_bbObjCompare");
			linkSymbol("_bbObjNext");
			linkSymbol("_bbObjPrev");
			linkSymbol("_bbObjFirst");
			linkSymbol("_bbObjLast");
			linkSymbol("_bbObjInsBefore");
			linkSymbol("_bbObjInsAfter");
			linkSymbol("_bbObjEachFirst");
			linkSymbol("_bbObjEachNext");
			linkSymbol("_bbObjEachFirst2");
			linkSymbol("_bbObjEachNext2");
			linkSymbol("_bbObjToStr");
			linkSymbol("_bbObjToHandle");
			linkSymbol("_bbObjFromHandle");
			linkSymbol("_bbNullObjEx");
			linkSymbol("_bbRestore");
			linkSymbol("_bbReadInt");
			linkSymbol("_bbReadFloat");
			linkSymbol("_bbReadStr");
			linkSymbol("_bbAbs");
			linkSymbol("_bbSgn");
			linkSymbol("_bbMod");
			linkSymbol("_bbFAbs");
			linkSymbol("_bbFSgn");
			linkSymbol("_bbFMod");
			linkSymbol("_bbFPow");
			linkSymbol("RuntimeStats");
		}

		private static void Math_link(LinkSymbol linkSymbol)
		{
			linkSymbol("#Sin#degrees");
			linkSymbol("#Cos#degrees");
			linkSymbol("#Tan#degrees");
			linkSymbol("#ASin#float");
			linkSymbol("#ACos#float");
			linkSymbol("#ATan#float");
			linkSymbol("#ATan2#floata#floatb");
			linkSymbol("#Sqr#float");
			linkSymbol("#Floor#float");
			linkSymbol("#Ceil#float");
			linkSymbol("#Exp#float");
			linkSymbol("#Log#float");
			linkSymbol("#Log10#float");
			linkSymbol("#Rnd#from#to=0");
			linkSymbol("%Rand%from%to=1");
			linkSymbol("SeedRnd%seed");
			linkSymbol("%RndSeed");
		}

		private static void String_link(LinkSymbol linkSymbol)
		{
			linkSymbol("$String$string%repeat");
			linkSymbol("$Left$string%count");
			linkSymbol("$Right$string%count");
			linkSymbol("$Replace$string$from$to");
			linkSymbol("%Instr$string$find%from=1");
			linkSymbol("$Mid$string%start%count=-1");
			linkSymbol("$Upper$string");
			linkSymbol("$Lower$string");
			linkSymbol("$Trim$string");
			linkSymbol("$LSet$string%size");
			linkSymbol("$RSet$string%size");
			linkSymbol("$Chr%ascii");
			linkSymbol("%Asc$string");
			linkSymbol("%Len$string");
			linkSymbol("$Hex%value");
			linkSymbol("$Bin%value");
			linkSymbol("$CurrentDate");
			linkSymbol("$CurrentTime");
		}

		private static void Stream_link(LinkSymbol linkSymbol)
		{
			linkSymbol("%Eof%stream");
			linkSymbol("%ReadAvail%stream");
			linkSymbol("%ReadByte%stream");
			linkSymbol("%ReadShort%stream");
			linkSymbol("%ReadInt%stream");
			linkSymbol("#ReadFloat%stream");
			linkSymbol("$ReadString%stream");
			linkSymbol("$ReadLine%stream");
			linkSymbol("WriteByte%stream%byte");
			linkSymbol("WriteShort%stream%short");
			linkSymbol("WriteInt%stream%int");
			linkSymbol("WriteFloat%stream#float");
			linkSymbol("WriteString%stream$string");
			linkSymbol("WriteLine%stream$string");
			linkSymbol("CopyStream%src_stream%dest_stream%buffer_size=16384");
		}

		private static void Sockets_link(LinkSymbol linkSymbol)
		{
			linkSymbol("$DottedIP%IP");
			linkSymbol("%CountHostIPs$host_name");
			linkSymbol("%HostIP%host_index");

			linkSymbol("%CreateUDPStream%port=0");
			linkSymbol("CloseUDPStream%udp_stream");
			linkSymbol("SendUDPMsg%udp_stream%dest_ip%dest_port=0");
			linkSymbol("%RecvUDPMsg%udp_stream");
			linkSymbol("%UDPStreamIP%udp_stream");
			linkSymbol("%UDPStreamPort%udp_stream");
			linkSymbol("%UDPMsgIP%udp_stream");
			linkSymbol("%UDPMsgPort%udp_stream");
			linkSymbol("UDPTimeouts%recv_timeout");

			linkSymbol("%OpenTCPStream$server%server_port%local_port=0");
			linkSymbol("CloseTCPStream%tcp_stream");
			linkSymbol("%CreateTCPServer%port");
			linkSymbol("CloseTCPServer%tcp_server");
			linkSymbol("%AcceptTCPStream%tcp_server");
			linkSymbol("%TCPStreamIP%tcp_stream");
			linkSymbol("%TCPStreamPort%tcp_stream");
			linkSymbol("TCPTimeouts%read_millis%accept_millis");
		}

		private static void Filesystem_link(LinkSymbol linkSymbol)
		{
			linkSymbol("%OpenFile$filename");
			linkSymbol("%ReadFile$filename");
			linkSymbol("%WriteFile$filename");
			linkSymbol("CloseFile%file_stream");
			linkSymbol("%FilePos%file_stream");
			linkSymbol("%SeekFile%file_stream%pos");

			linkSymbol("%ReadDir$dirname");
			linkSymbol("CloseDir%dir");
			linkSymbol("$NextFile%dir");
			linkSymbol("$CurrentDir");
			linkSymbol("ChangeDir$dir");
			linkSymbol("CreateDir$dir");
			linkSymbol("DeleteDir$dir");

			linkSymbol("%FileSize$file");
			linkSymbol("%FileType$file");
			linkSymbol("CopyFile$file$to");
			linkSymbol("DeleteFile$file");
		}

		private static void Bank_link(LinkSymbol linkSymbol)
		{
			linkSymbol("%CreateBank%size=0");
			linkSymbol("FreeBank%bank");
			linkSymbol("%BankSize%bank");
			linkSymbol("ResizeBank%bank%size");
			linkSymbol("CopyBank%src_bank%src_offset%dest_bank%dest_offset%count");
			linkSymbol("%PeekByte%bank%offset");
			linkSymbol("%PeekShort%bank%offset");
			linkSymbol("%PeekInt%bank%offset");
			linkSymbol("#PeekFloat%bank%offset");
			linkSymbol("PokeByte%bank%offset%value");
			linkSymbol("PokeShort%bank%offset%value");
			linkSymbol("PokeInt%bank%offset%value");
			linkSymbol("PokeFloat%bank%offset#value");
			linkSymbol("%ReadBytes%bank%file%offset%count");
			linkSymbol("%WriteBytes%bank%file%offset%count");
			linkSymbol("%CallDLL$dll_name$func_name%in_bank=0%out_bank=0");
		}

		private static void Graphics_link(LinkSymbol linkSymbol)
		{
			//gfx driver info
			linkSymbol("%CountGfxDrivers");
			linkSymbol("$GfxDriverName%driver");
			linkSymbol("SetGfxDriver%driver");

			//gfx mode info
			linkSymbol("%CountGfxModes");
			linkSymbol("%GfxModeExists%width%height%depth");

			linkSymbol("%GfxModeWidth%mode");
			linkSymbol("%GfxModeHeight%mode");
			linkSymbol("%GfxModeDepth%mode");
			linkSymbol("%AvailVidMem");
			linkSymbol("%TotalVidMem");

			linkSymbol("%GfxDriver3D%driver");
			linkSymbol("%CountGfxModes3D");
			linkSymbol("%GfxMode3DExists%width%height%depth");
			linkSymbol("%GfxMode3D%mode");
			linkSymbol("%Windowed3D");

			//display mode
			linkSymbol("Graphics%width%height%depth=0%mode=0");
			linkSymbol("Graphics3D%width%height%depth=0%mode=0");
			linkSymbol("EndGraphics");
			linkSymbol("%GraphicsLost");

			linkSymbol("SetGamma%src_red%src_green%src_blue#dest_red#dest_green#dest_blue");
			linkSymbol("UpdateGamma%calibrate=0");
			linkSymbol("#GammaRed%red");
			linkSymbol("#GammaGreen%green");
			linkSymbol("#GammaBlue%blue");

			linkSymbol("%FrontBuffer");
			linkSymbol("%BackBuffer");
			linkSymbol("%ScanLine");
			linkSymbol("VWait%frames=1");
			linkSymbol("Flip%vwait=1");
			linkSymbol("%GraphicsWidth");
			linkSymbol("%GraphicsHeight");
			linkSymbol("%GraphicsDepth");

			//buffer management
			linkSymbol("SetBuffer%buffer");
			linkSymbol("%GraphicsBuffer");
			linkSymbol("%LoadBuffer%buffer$bmpfile");
			linkSymbol("%SaveBuffer%buffer$bmpfile");
			linkSymbol("BufferDirty%buffer");

			//fast pixel reads/write
			linkSymbol("LockBuffer%buffer=0");
			linkSymbol("UnlockBuffer%buffer=0");
			linkSymbol("%ReadPixel%x%y%buffer=0");
			linkSymbol("WritePixel%x%y%argb%buffer=0");
			linkSymbol("%ReadPixelFast%x%y%buffer=0");
			linkSymbol("WritePixelFast%x%y%argb%buffer=0");
			linkSymbol("CopyPixel%src_x%src_y%src_buffer%dest_x%dest_y%dest_buffer=0");
			linkSymbol("CopyPixelFast%src_x%src_y%src_buffer%dest_x%dest_y%dest_buffer=0");

			//rendering
			linkSymbol("Origin%x%y");
			linkSymbol("Viewport%x%y%width%height");
			linkSymbol("Color%red%green%blue");
			linkSymbol("GetColor%x%y");
			linkSymbol("%ColorRed");
			linkSymbol("%ColorGreen");
			linkSymbol("%ColorBlue");
			linkSymbol("ClsColor%red%green%blue");
			linkSymbol("SetFont%font");
			linkSymbol("Cls");
			linkSymbol("Plot%x%y");
			linkSymbol("Rect%x%y%width%height%solid=1");
			linkSymbol("Oval%x%y%width%height%solid=1");
			linkSymbol("Line%x1%y1%x2%y2");
			linkSymbol("Text%x%y$text%centre_x=0%centre_y=0");
			linkSymbol("CopyRect%source_x%source_y%width%height%dest_x%dest_y%src_buffer=0%dest_buffer=0");

			//fonts
			linkSymbol("%LoadFont$fontname%height=12%bold=0%italic=0%underline=0");
			linkSymbol("FreeFont%font");
			linkSymbol("%FontWidth");
			linkSymbol("%FontHeight");
			linkSymbol("%StringWidth$string");
			linkSymbol("%StringHeight$string");

			//movies
			linkSymbol("%OpenMovie$file");
			linkSymbol("%DrawMovie%movie%x=0%y=0%w=-1%h=-1");
			linkSymbol("%MovieWidth%movie");
			linkSymbol("%MovieHeight%movie");
			linkSymbol("%MoviePlaying%movie");
			linkSymbol("CloseMovie%movie");

			linkSymbol("%LoadImage$bmpfile");
			linkSymbol("%LoadAnimImage$bmpfile%cellwidth%cellheight%first%count");
			linkSymbol("%CopyImage%image");
			linkSymbol("%CreateImage%width%height%frames=1");
			linkSymbol("FreeImage%image");
			linkSymbol("%SaveImage%image$bmpfile%frame=0");

			linkSymbol("GrabImage%image%x%y%frame=0");
			linkSymbol("%ImageBuffer%image%frame=0");
			linkSymbol("DrawImage%image%x%y%frame=0");
			linkSymbol("DrawBlock%image%x%y%frame=0");
			linkSymbol("TileImage%image%x=0%y=0%frame=0");
			linkSymbol("TileBlock%image%x=0%y=0%frame=0");
			linkSymbol("DrawImageRect%image%x%y%rect_x%rect_y%rect_width%rect_height%frame=0");
			linkSymbol("DrawBlockRect%image%x%y%rect_x%rect_y%rect_width%rect_height%frame=0");
			linkSymbol("MaskImage%image%red%green%blue");
			linkSymbol("HandleImage%image%x%y");
			linkSymbol("MidHandle%image");
			linkSymbol("AutoMidHandle%enable");
			linkSymbol("%ImageWidth%image");
			linkSymbol("%ImageHeight%image");
			linkSymbol("%ImageXHandle%image");
			linkSymbol("%ImageYHandle%image");

			linkSymbol("ScaleImage%image#xscale#yscale");
			linkSymbol("ResizeImage%image#width#height");
			linkSymbol("RotateImage%image#angle");
			linkSymbol("TFormImage%image#a#b#c#d");
			linkSymbol("TFormFilter%enable");

			linkSymbol("%ImagesOverlap%image1%x1%y1%image2%x2%y2");
			linkSymbol("%ImagesCollide%image1%x1%y1%frame1%image2%x2%y2%frame2");
			linkSymbol("%RectsOverlap%x1%y1%width1%height1%x2%y2%width2%height2");
			linkSymbol("%ImageRectOverlap%image%x%y%rect_x%rect_y%rect_width%rect_height");
			linkSymbol("%ImageRectCollide%image%x%y%frame%rect_x%rect_y%rect_width%rect_height");

			linkSymbol("Write$string");
			linkSymbol("Print$string=\"\"");
			linkSymbol("$Input$prompt=\"\"");
			linkSymbol("Locate%x%y");

			linkSymbol("ShowPointer");
			linkSymbol("HidePointer");
		}

		private static void Input_link(LinkSymbol linkSymbol)
		{
			linkSymbol("%KeyDown%key");
			linkSymbol("%KeyHit%key");
			linkSymbol("%GetKey");
			linkSymbol("%WaitKey");
			linkSymbol("FlushKeys");

			linkSymbol("%MouseDown%button");
			linkSymbol("%MouseHit%button");
			linkSymbol("%GetMouse");
			linkSymbol("%WaitMouse");
			linkSymbol("%MouseWait");
			linkSymbol("%MouseX");
			linkSymbol("%MouseY");
			linkSymbol("%MouseZ");
			linkSymbol("%MouseXSpeed");
			linkSymbol("%MouseYSpeed");
			linkSymbol("%MouseZSpeed");
			linkSymbol("FlushMouse");
			linkSymbol("MoveMouse%x%y");

			linkSymbol("%JoyType%port=0");
			linkSymbol("%JoyDown%button%port=0");
			linkSymbol("%JoyHit%button%port=0");
			linkSymbol("%GetJoy%port=0");
			linkSymbol("%WaitJoy%port=0");
			linkSymbol("%JoyWait%port=0");
			linkSymbol("#JoyX%port=0");
			linkSymbol("#JoyY%port=0");
			linkSymbol("#JoyZ%port=0");
			linkSymbol("#JoyU%port=0");
			linkSymbol("#JoyV%port=0");
			linkSymbol("#JoyPitch%port=0");
			linkSymbol("#JoyYaw%port=0");
			linkSymbol("#JoyRoll%port=0");
			linkSymbol("%JoyHat%port=0");
			linkSymbol("%JoyXDir%port=0");
			linkSymbol("%JoyYDir%port=0");
			linkSymbol("%JoyZDir%port=0");
			linkSymbol("%JoyUDir%port=0");
			linkSymbol("%JoyVDir%port=0");
			linkSymbol("FlushJoy");

			linkSymbol("EnableDirectInput%enable");
			linkSymbol("%DirectInputEnabled");
		}

		private static void Audio_link(LinkSymbol linkSymbol)
		{
			linkSymbol("%LoadSound$filename");
			linkSymbol("FreeSound%sound");
			linkSymbol("LoopSound%sound");
			linkSymbol("SoundPitch%sound%pitch");
			linkSymbol("SoundVolume%sound#volume");
			linkSymbol("SoundPan%sound#pan");
			linkSymbol("%PlaySound%sound");
			linkSymbol("%PlayMusic$midifile");
			linkSymbol("%PlayCDTrack%track%mode=1");
			linkSymbol("StopChannel%channel");
			linkSymbol("PauseChannel%channel");
			linkSymbol("ResumeChannel%channel");
			linkSymbol("ChannelPitch%channel%pitch");
			linkSymbol("ChannelVolume%channel#volume");
			linkSymbol("ChannelPan%channel#pan");
			linkSymbol("%ChannelPlaying%channel");
			linkSymbol("%Load3DSound$filename");
		}

		private static void Multiplay_link(LinkSymbol linkSymbol)
		{
			linkSymbol("%StartNetGame");
			linkSymbol("%HostNetGame$game_name");
			linkSymbol("%JoinNetGame$game_name$ip_address");
			linkSymbol("StopNetGame");

			linkSymbol("%CreateNetPlayer$name");
			linkSymbol("DeleteNetPlayer%player");
			linkSymbol("$NetPlayerName%player");
			linkSymbol("%NetPlayerLocal%player");

			linkSymbol("%SendNetMsg%type$msg%from_player%to_player=0%reliable=1");

			linkSymbol("%RecvNetMsg");
			linkSymbol("%NetMsgType");
			linkSymbol("%NetMsgFrom");
			linkSymbol("%NetMsgTo");
			linkSymbol("$NetMsgData");
		}

		private static void Userlibs_link(LinkSymbol linkSymbol)
		{
			linkSymbol("_bbLoadLibs");
			linkSymbol("_bbStrToCStr");
			linkSymbol("_bbCStrToStr");
		}

		private static void Blitz3d_link(LinkSymbol linkSymbol)
		{
			linkSymbol("LoaderMatrix$file_ext#xx#xy#xz#yx#yy#yz#zx#zy#zz");
			linkSymbol("HWMultiTex%enable");
			linkSymbol("%HWTexUnits");
			linkSymbol("%GfxDriverCaps3D");
			linkSymbol("WBuffer%enable");
			linkSymbol("Dither%enable");
			linkSymbol("AntiAlias%enable");
			linkSymbol("WireFrame%enable");
			linkSymbol("AmbientLight#red#green#blue");
			linkSymbol("ClearCollisions");
			linkSymbol("Collisions%source_type%destination_type%method%response");
			linkSymbol("UpdateWorld#elapsed_time=1");
			linkSymbol("CaptureWorld");
			linkSymbol("RenderWorld#tween=1");
			linkSymbol("ClearWorld%entities=1%brushes=1%textures=1");
			linkSymbol("%ActiveTextures");
			linkSymbol("%TrisRendered");
			linkSymbol("#Stats3D%type");

			linkSymbol("%CreateTexture%width%height%flags=0%frames=1");
			linkSymbol("%LoadTexture$file%flags=1");
			linkSymbol("%LoadAnimTexture$file%flags%width%height%first%count");
			linkSymbol("FreeTexture%texture");
			linkSymbol("TextureBlend%texture%blend");
			linkSymbol("TextureCoords%texture%coords");
			linkSymbol("ScaleTexture%texture#u_scale#v_scale");
			linkSymbol("RotateTexture%texture#angle");
			linkSymbol("PositionTexture%texture#u_offset#v_offset");
			linkSymbol("%TextureWidth%texture");
			linkSymbol("%TextureHeight%texture");
			linkSymbol("$TextureName%texture");
			linkSymbol("SetCubeFace%texture%face");
			linkSymbol("SetCubeMode%texture%mode");
			linkSymbol("%TextureBuffer%texture%frame=0");
			linkSymbol("ClearTextureFilters");
			linkSymbol("TextureFilter$match_text%texture_flags=0");

			linkSymbol("%CreateBrush#red=255#green=255#blue=255");
			linkSymbol("%LoadBrush$file%texture_flags=1#u_scale=1#v_scale=1");
			linkSymbol("FreeBrush%brush");
			linkSymbol("BrushColor%brush#red#green#blue");
			linkSymbol("BrushAlpha%brush#alpha");
			linkSymbol("BrushShininess%brush#shininess");
			linkSymbol("BrushTexture%brush%texture%frame=0%index=0");
			linkSymbol("%GetBrushTexture%brush%index=0");
			linkSymbol("BrushBlend%brush%blend");
			linkSymbol("BrushFX%brush%fx");

			linkSymbol("%LoadMesh$file%parent=0");
			linkSymbol("%LoadAnimMesh$file%parent=0");
			linkSymbol("%LoadAnimSeq%entity$file");

			linkSymbol("%CreateMesh%parent=0");
			linkSymbol("%CreateCube%parent=0");
			linkSymbol("%CreateSphere%segments=8%parent=0");
			linkSymbol("%CreateCylinder%segments=8%solid=1%parent=0");
			linkSymbol("%CreateCone%segments=8%solid=1%parent=0");
			linkSymbol("%CopyMesh%mesh%parent=0");
			linkSymbol("ScaleMesh%mesh#x_scale#y_scale#z_scale");
			linkSymbol("RotateMesh%mesh#pitch#yaw#roll");
			linkSymbol("PositionMesh%mesh#x#y#z");
			linkSymbol("FitMesh%mesh#x#y#z#width#height#depth%uniform=0");
			linkSymbol("FlipMesh%mesh");
			linkSymbol("PaintMesh%mesh%brush");
			linkSymbol("AddMesh%source_mesh%dest_mesh");
			linkSymbol("UpdateNormals%mesh");
			linkSymbol("LightMesh%mesh#red#green#blue#range=0#x=0#y=0#z=0");
			linkSymbol("#MeshWidth%mesh");
			linkSymbol("#MeshHeight%mesh");
			linkSymbol("#MeshDepth%mesh");
			linkSymbol("%MeshesIntersect%mesh_a%mesh_b");
			linkSymbol("%CountSurfaces%mesh");
			linkSymbol("%GetSurface%mesh%surface_index");
			linkSymbol("MeshCullBox%mesh#x#y#z#width#height#depth");

			linkSymbol("%CreateSurface%mesh%brush=0");
			linkSymbol("%GetSurfaceBrush%surface");
			linkSymbol("%GetEntityBrush%entity");
			linkSymbol("%FindSurface%mesh%brush");
			linkSymbol("ClearSurface%surface%clear_vertices=1%clear_triangles=1");
			linkSymbol("PaintSurface%surface%brush");
			linkSymbol("%AddVertex%surface#x#y#z#u=0#v=0#w=1");
			linkSymbol("%AddTriangle%surface%v0%v1%v2");
			linkSymbol("VertexCoords%surface%index#x#y#z");
			linkSymbol("VertexNormal%surface%index#nx#ny#nz");
			linkSymbol("VertexColor%surface%index#red#green#blue#alpha=1");
			linkSymbol("VertexTexCoords%surface%index#u#v#w=1%coord_set=0");
			linkSymbol("%CountVertices%surface");
			linkSymbol("%CountTriangles%surface");
			linkSymbol("#VertexX%surface%index");
			linkSymbol("#VertexY%surface%index");
			linkSymbol("#VertexZ%surface%index");
			linkSymbol("#VertexNX%surface%index");
			linkSymbol("#VertexNY%surface%index");
			linkSymbol("#VertexNZ%surface%index");
			linkSymbol("#VertexRed%surface%index");
			linkSymbol("#VertexGreen%surface%index");
			linkSymbol("#VertexBlue%surface%index");
			linkSymbol("#VertexAlpha%surface%index");
			linkSymbol("#VertexU%surface%index%coord_set=0");
			linkSymbol("#VertexV%surface%index%coord_set=0");
			linkSymbol("#VertexW%surface%index%coord_set=0");
			linkSymbol("%TriangleVertex%surface%index%vertex");

			linkSymbol("%CreateCamera%parent=0");
			linkSymbol("CameraZoom%camera#zoom");
			linkSymbol("CameraRange%camera#near#far");
			linkSymbol("CameraClsColor%camera#red#green#blue");
			linkSymbol("CameraClsMode%camera%cls_color%cls_zbuffer");
			linkSymbol("CameraProjMode%camera%mode");
			linkSymbol("CameraViewport%camera%x%y%width%height");
			linkSymbol("CameraFogColor%camera#red#green#blue");
			linkSymbol("CameraFogRange%camera#near#far");
			linkSymbol("CameraFogMode%camera%mode");
			linkSymbol("CameraProject%camera#x#y#z");
			linkSymbol("#ProjectedX");
			linkSymbol("#ProjectedY");
			linkSymbol("#ProjectedZ");

			linkSymbol("%EntityInView%entity%camera");
			linkSymbol("%EntityVisible%src_entity%dest_entity");

			linkSymbol("%EntityPick%entity#range");
			linkSymbol("%LinePick#x#y#z#dx#dy#dz#radius=0");
			linkSymbol("%CameraPick%camera#viewport_x#viewport_y");

			linkSymbol("#PickedX");
			linkSymbol("#PickedY");
			linkSymbol("#PickedZ");
			linkSymbol("#PickedNX");
			linkSymbol("#PickedNY");
			linkSymbol("#PickedNZ");
			linkSymbol("#PickedTime");
			linkSymbol("%PickedEntity");
			linkSymbol("%PickedSurface");
			linkSymbol("%PickedTriangle");

			linkSymbol("%CreateLight%type=1%parent=0");
			linkSymbol("LightColor%light#red#green#blue");
			linkSymbol("LightRange%light#range");
			linkSymbol("LightConeAngles%light#inner_angle#outer_angle");

			linkSymbol("%CreatePivot%parent=0");

			linkSymbol("%CreateSprite%parent=0");
			linkSymbol("%LoadSprite$file%texture_flags=1%parent=0");
			linkSymbol("RotateSprite%sprite#angle");
			linkSymbol("ScaleSprite%sprite#x_scale#y_scale");
			linkSymbol("HandleSprite%sprite#x_handle#y_handle");
			linkSymbol("SpriteViewMode%sprite%view_mode");

			linkSymbol("%LoadMD2$file%parent=0");
			linkSymbol("AnimateMD2%md2%mode=1#speed=1%first_frame=0%last_frame=9999#transition=0");
			linkSymbol("#MD2AnimTime%md2");
			linkSymbol("%MD2AnimLength%md2");
			linkSymbol("%MD2Animating%md2");

			linkSymbol("%LoadBSP$file#gamma_adj=0%parent=0");
			linkSymbol("BSPLighting%bsp%use_lightmaps");
			linkSymbol("BSPAmbientLight%bsp#red#green#blue");

			linkSymbol("%CreateMirror%parent=0");

			linkSymbol("%CreatePlane%segments=1%parent=0");

			linkSymbol("%CreateTerrain%grid_size%parent=0");
			linkSymbol("%LoadTerrain$heightmap_file%parent=0");
			linkSymbol("TerrainDetail%terrain%detail_level%morph=0");
			linkSymbol("TerrainShading%terrain%enable");
			linkSymbol("#TerrainX%terrain#world_x#world_y#world_z");
			linkSymbol("#TerrainY%terrain#world_x#world_y#world_z");
			linkSymbol("#TerrainZ%terrain#world_x#world_y#world_z");
			linkSymbol("%TerrainSize%terrain");
			linkSymbol("#TerrainHeight%terrain%terrain_x%terrain_z");
			linkSymbol("ModifyTerrain%terrain%terrain_x%terrain_z#height%realtime=0");

			linkSymbol("%CreateListener%parent#rolloff_factor=1#doppler_scale=1#distance_scale=1");
			linkSymbol("%EmitSound%sound%entity");

			linkSymbol("%CopyEntity%entity%parent=0");

			linkSymbol("#EntityX%entity%global=0");
			linkSymbol("#EntityY%entity%global=0");
			linkSymbol("#EntityZ%entity%global=0");
			linkSymbol("#EntityPitch%entity%global=0");
			linkSymbol("#EntityYaw%entity%global=0");
			linkSymbol("#EntityRoll%entity%global=0");
			linkSymbol("#GetMatElement%entity%row%column");
			linkSymbol("TFormPoint#x#y#z%source_entity%dest_entity");
			linkSymbol("TFormVector#x#y#z%source_entity%dest_entity");
			linkSymbol("TFormNormal#x#y#z%source_entity%dest_entity");
			linkSymbol("#TFormedX");
			linkSymbol("#TFormedY");
			linkSymbol("#TFormedZ");
			linkSymbol("#VectorYaw#x#y#z");
			linkSymbol("#VectorPitch#x#y#z");
			linkSymbol("#DeltaPitch%src_entity%dest_entity");
			linkSymbol("#DeltaYaw%src_entity%dest_entity");

			linkSymbol("ResetEntity%entity");
			linkSymbol("EntityType%entity%collision_type%recursive=0");
			linkSymbol("EntityPickMode%entity%pick_geometry%obscurer=1");
			linkSymbol("%GetParent%entity");
			linkSymbol("%GetEntityType%entity");
			linkSymbol("EntityRadius%entity#x_radius#y_radius=0");
			linkSymbol("EntityBox%entity#x#y#z#width#height#depth");
			linkSymbol("#EntityDistance%source_entity%destination_entity");
			linkSymbol("%EntityCollided%entity%type");

			linkSymbol("%CountCollisions%entity");
			linkSymbol("#CollisionX%entity%collision_index");
			linkSymbol("#CollisionY%entity%collision_index");
			linkSymbol("#CollisionZ%entity%collision_index");
			linkSymbol("#CollisionNX%entity%collision_index");
			linkSymbol("#CollisionNY%entity%collision_index");
			linkSymbol("#CollisionNZ%entity%collision_index");
			linkSymbol("#CollisionTime%entity%collision_index");
			linkSymbol("%CollisionEntity%entity%collision_index");
			linkSymbol("%CollisionSurface%entity%collision_index");
			linkSymbol("%CollisionTriangle%entity%collision_index");

			linkSymbol("MoveEntity%entity#x#y#z");
			linkSymbol("TurnEntity%entity#pitch#yaw#roll%global=0");
			linkSymbol("TranslateEntity%entity#x#y#z%global=0");
			linkSymbol("PositionEntity%entity#x#y#z%global=0");
			linkSymbol("ScaleEntity%entity#x_scale#y_scale#z_scale%global=0");
			linkSymbol("RotateEntity%entity#pitch#yaw#roll%global=0");
			linkSymbol("PointEntity%entity%target#roll=0");
			linkSymbol("AlignToVector%entity#vector_x#vector_y#vector_z%axis#rate=1");
			linkSymbol("SetAnimTime%entity#time%anim_seq=0");
			linkSymbol("Animate%entity%mode=1#speed=1%sequence=0#transition=0");
			linkSymbol("SetAnimKey%entity%frame%pos_key=1%rot_key=1%scale_key=1");
			linkSymbol("%AddAnimSeq%entity%length");
			linkSymbol("%ExtractAnimSeq%entity%first_frame%last_frame%anim_seq=0");
			linkSymbol("%AnimSeq%entity");
			linkSymbol("#AnimTime%entity");
			linkSymbol("%AnimLength%entity");
			linkSymbol("%Animating%entity");

			linkSymbol("EntityParent%entity%parent%global=1");
			linkSymbol("%CountChildren%entity");
			linkSymbol("%GetChild%entity%index");
			linkSymbol("%FindChild%entity$name");

			linkSymbol("PaintEntity%entity%brush");
			linkSymbol("EntityColor%entity#red#green#blue");
			linkSymbol("EntityAlpha%entity#alpha");
			linkSymbol("EntityShininess%entity#shininess");
			linkSymbol("EntityTexture%entity%texture%frame=0%index=0");
			linkSymbol("EntityBlend%entity%blend");
			linkSymbol("EntityFX%entity%fx");
			linkSymbol("EntityAutoFade%entity#near#far");
			linkSymbol("EntityOrder%entity%order");
			linkSymbol("HideEntity%entity");
			linkSymbol("ShowEntity%entity");
			linkSymbol("FreeEntity%entity");

			linkSymbol("NameEntity%entity$name");
			linkSymbol("$EntityName%entity");
			linkSymbol("$EntityClass%entity");
		}

		private static void DllFunctions(LinkSymbol linkSymbol)
		{
			linkSymbol("%getactivewindow");//Was renamed to HasFocus

			linkSymbol("%HasFocus");
			linkSymbol("%GetSpecialFolder%id%bank");
			linkSymbol("api_MoveFile$a$b");
			linkSymbol("%api_GlobalAlloc%a%b");
		}

		private static void LinkSymbols(LinkSymbol linkSymbol)
		{
			Runtime_link(linkSymbol);
			Basic_link(linkSymbol);
			Math_link(linkSymbol);
			String_link(linkSymbol);
			Stream_link(linkSymbol);
			Sockets_link(linkSymbol);
			Filesystem_link(linkSymbol);
			Bank_link(linkSymbol);
			Graphics_link(linkSymbol);
			Input_link(linkSymbol);
			Audio_link(linkSymbol);
			Multiplay_link(linkSymbol);
			Blitz3d_link(linkSymbol);
			Userlibs_link(linkSymbol);

			//Was commented out. Why?
			DllFunctions(linkSymbol);
		}

		public static HashSet<string> GetLinkSymbols()
		{
			HashSet<string> ret = new HashSet<string>();
			LinkSymbols(((ICollection<string>)ret).Add);
			return ret;
		}
	}
}