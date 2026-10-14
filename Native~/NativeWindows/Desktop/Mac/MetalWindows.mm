// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#include "WindowBackend.h"
#include "MacCoordinates.h"
#include "IUnityGraphicsMetal.h"
#import <Metal/Metal.h>
#import <QuartzCore/CAMetalLayer.h>
#include <atomic>
#include <mutex>
#include <stdexcept>

namespace
{
	IUnityGraphicsMetal* metal = nullptr;
	id<MTLDevice> device = nil;
	id<MTLRenderPipelineState> pipeline = nil;
	constexpr const char* Shader = R"(
#include <metal_stdlib>
using namespace metal;
struct Varying { float4 position [[position]]; float2 uv; };
vertex Varying vpnw_vertex(uint id [[vertex_id]], constant uint& flip [[buffer(0)]]) {
    float2 uv = float2((id << 1) & 2, id & 2);
    Varying output; output.position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    output.uv = float2(uv.x, flip ? 1 - uv.y : uv.y); return output;
}
fragment float4 vpnw_fragment(Varying input [[stage_in]], texture2d<float> source [[texture(0)]]) {
    constexpr sampler sample(filter::linear, address::clamp_to_edge);
    return source.sample(sample, input.uv);
})";
	void Pipeline()
	{
		if (pipeline) return;
		NSError* error = nil;
		id<MTLLibrary> library = [device newLibraryWithSource:[NSString stringWithUTF8String:Shader] options:nil error:&error];
		if (!library) throw std::runtime_error(error.localizedDescription.UTF8String ?: "Could not compile desktop Metal shaders.");
		MTLRenderPipelineDescriptor* description = [[MTLRenderPipelineDescriptor alloc] init];
		description.vertexFunction = [library newFunctionWithName:@"vpnw_vertex"];
		description.fragmentFunction = [library newFunctionWithName:@"vpnw_fragment"];
		description.colorAttachments[0].pixelFormat = MTLPixelFormatBGRA8Unorm;
		description.colorAttachments[0].blendingEnabled = NO;
		pipeline = [device newRenderPipelineStateWithDescriptor:description error:&error];
		if (!pipeline) throw std::runtime_error(error.localizedDescription.UTF8String ?: "Could not create desktop Metal pipeline.");
	}
	class MetalWindow final : public vpnw::WindowBackend, public std::enable_shared_from_this<MetalWindow>
	{
		std::mutex mutex;
		NSWindow* window = nil;
		CAMetalLayer* layer = nil;
		id<MTLTexture> texture = nil;
		id<MTLTexture> rawSnapshot = nil;
		std::atomic<int> inFlight{ 0 };
		bool alive = true, visible = false, topmost = false, flip = false, closing = false;
		int left, top, width, height;
		std::uint64_t presented = 0, skipped = 0;
	public:
		MetalWindow(int x, int y, int w, int h) : left(x), top(y), width(w), height(h)
		{
			if (!metal || !device) throw std::runtime_error("Unity Metal is required for macOS desktop output.");
			vpnw::MainThread(^{
				window = [[NSWindow alloc] initWithContentRect:vpnw::CocoaFrame(x, y, w, h) styleMask:NSWindowStyleMaskBorderless backing:NSBackingStoreBuffered defer:NO];
				window.releasedWhenClosed = NO; window.opaque = NO; window.backgroundColor = NSColor.clearColor;
				window.hasShadow = NO; window.title = @"Visual Pinball Window";
				window.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorStationary;
				layer = [CAMetalLayer layer]; layer.device = device; layer.pixelFormat = MTLPixelFormatBGRA8Unorm;
				CGColorSpaceRef sdr = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
				layer.colorspace = sdr; CGColorSpaceRelease(sdr);
				layer.opaque = NO; layer.framebufferOnly = YES; layer.maximumDrawableCount = 3;
				layer.allowsNextDrawableTimeout = YES; layer.displaySyncEnabled = NO;
				layer.drawableSize = CGSizeMake(w, h); layer.contentsScale = window.backingScaleFactor;
				window.contentView.wantsLayer = YES; window.contentView.layer = layer;
			});
		}
		~MetalWindow() override
		{
			NSWindow* owned = window;
			if (owned) dispatch_async(dispatch_get_main_queue(), ^{ [owned orderOut:nil]; [owned close]; });
		}
		bool Configure(int x, int y, int w, int h, bool show, bool stayOnTop) override
		{
			std::lock_guard<std::mutex> lock(mutex);
			left = x; top = y; width = w; height = h; visible = show; topmost = stayOnTop;
			vpnw::MainThread(^{
				[window setFrame:vpnw::CocoaFrame(x, y, w, h) display:NO];
				window.level = stayOnTop ? NSFloatingWindowLevel : NSNormalWindowLevel;
				layer.contentsScale = window.backingScaleFactor; layer.drawableSize = CGSizeMake(w, h);
				if (show) [window orderFrontRegardless]; else [window orderOut:nil];
			});
			return true;
		}
		bool SetOrder(int order) override
		{
			std::lock_guard<std::mutex> lock(mutex);
			if (order >= 2) topmost = order == 2;
			vpnw::MainThread(^{
				if (order == 0) [window orderBack:nil]; else if (order == 1) [window orderFrontRegardless];
				else window.level = order == 2 ? NSFloatingWindowLevel : NSNormalWindowLevel;
			}); return true;
		}
		bool SetTexture(void* value, bool flipY) override
		{
			std::lock_guard<std::mutex> lock(mutex); texture = (__bridge id<MTLTexture>)value; rawSnapshot = nil; flip = flipY; return true;
		}
		bool SetPlacementMode(bool enabled) override
		{
			vpnw::MainThread(^{
				window.movableByWindowBackground = enabled;
				window.styleMask = NSWindowStyleMaskBorderless | (enabled ? NSWindowStyleMaskResizable : 0);
			});
			return true;
		}
		void Retire() override
		{
			std::lock_guard<std::mutex> lock(mutex); alive = visible = false; texture = nil;
			NSWindow* owned = window; dispatch_async(dispatch_get_main_queue(), ^{ [owned orderOut:nil]; });
		}
		bool Collect() override
		{
			std::lock_guard<std::mutex> lock(mutex);
			if (inFlight.load() != 0) return false;
			if (!closing)
			{
				closing = true; NSWindow* owned = window; window = nil; layer = nil;
				dispatch_async(dispatch_get_main_queue(), ^{ [owned close]; });
			}
			return true;
		}
		void Draw() override {}
		void Present() override
		{
			std::lock_guard<std::mutex> lock(mutex);
			if (!alive || !visible || !texture || !metal || inFlight.load() >= 2) { skipped++; return; }
			@autoreleasepool
			{
				Pipeline(); id<CAMetalDrawable> drawable = [layer nextDrawable];
				if (!drawable) { skipped++; return; }
				metal->EndCurrentCommandEncoder(); id<MTLCommandBuffer> command = metal->CurrentCommandBuffer();
				if (!command) { skipped++; return; }
				id<MTLTexture> source = texture;
				// A raw GPU copy avoids sampling the sRGB view and preserves encoded SDR bytes.
				if (texture.pixelFormat == MTLPixelFormatBGRA8Unorm_sRGB || texture.pixelFormat == MTLPixelFormatRGBA8Unorm_sRGB)
				{
					MTLPixelFormat format = texture.pixelFormat == MTLPixelFormatBGRA8Unorm_sRGB ? MTLPixelFormatBGRA8Unorm : MTLPixelFormatRGBA8Unorm;
					if (!rawSnapshot || rawSnapshot.width != texture.width || rawSnapshot.height != texture.height)
					{
						MTLTextureDescriptor* description = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:format width:texture.width height:texture.height mipmapped:NO];
						description.storageMode = MTLStorageModePrivate; description.usage = MTLTextureUsageShaderRead;
						rawSnapshot = [device newTextureWithDescriptor:description];
					}
					id<MTLBlitCommandEncoder> copy = [command blitCommandEncoder];
					[copy copyFromTexture:texture sourceSlice:0 sourceLevel:0 sourceOrigin:MTLOriginMake(0, 0, 0) sourceSize:MTLSizeMake(texture.width, texture.height, 1)
						toTexture:rawSnapshot destinationSlice:0 destinationLevel:0 destinationOrigin:MTLOriginMake(0, 0, 0)]; [copy endEncoding]; source = rawSnapshot;
				}
				MTLRenderPassDescriptor* pass = [MTLRenderPassDescriptor renderPassDescriptor];
				pass.colorAttachments[0].texture = drawable.texture; pass.colorAttachments[0].loadAction = MTLLoadActionDontCare; pass.colorAttachments[0].storeAction = MTLStoreActionStore;
				id<MTLRenderCommandEncoder> encoder = [command renderCommandEncoderWithDescriptor:pass];
				[encoder setRenderPipelineState:pipeline]; [encoder setFragmentTexture:source atIndex:0];
				std::uint32_t inversion = flip ? 1 : 0; [encoder setVertexBytes:&inversion length:sizeof(inversion) atIndex:0];
				[encoder drawPrimitives:MTLPrimitiveTypeTriangle vertexStart:0 vertexCount:3]; [encoder endEncoding];
				[command presentDrawable:drawable]; inFlight++;
				auto owner = shared_from_this(); id<MTLTexture> retainedSource = source, retainedInput = texture;
				[command addCompletedHandler:^(id<MTLCommandBuffer> completed) {
					(void)retainedSource; (void)retainedInput;
					if (completed.status == MTLCommandBufferStatusError) vpnw::SetError(completed.error.localizedDescription.UTF8String ?: "Desktop Metal command failed.");
					owner->inFlight--;
				}];
				presented++;
			}
		}
		VPNW_Diagnostics Diagnostics() override
		{
			std::lock_guard<std::mutex> lock(mutex); VPNW_Diagnostics result{};
			result.structBytes = sizeof(result); result.graphicsRenderer = kUnityGfxRendererMetal; result.deviceReady = device ? 1 : 0;
			result.alive = alive ? 1 : 0; result.visible = visible ? 1 : 0; result.topmost = topmost ? 1 : 0;
			vpnw::MainThread(^{
				NSRect bounds = vpnw::PixelFrame(window);
				left = static_cast<int>(std::round(bounds.origin.x)); top = static_cast<int>(std::round(bounds.origin.y));
				width = static_cast<int>(std::round(bounds.size.width)); height = static_cast<int>(std::round(bounds.size.height));
				layer.contentsScale = window.backingScaleFactor; layer.drawableSize = CGSizeMake(width, height);
			});
			result.left = left; result.top = top; result.windowWidth = width; result.windowHeight = height; result.dpi = 96;
			result.sourceWidth = static_cast<std::uint32_t>(texture.width); result.sourceHeight = static_cast<std::uint32_t>(texture.height);
			result.sourceFormat = static_cast<std::uint32_t>(texture.pixelFormat); result.presentedFrames = presented; result.skippedFrames = skipped; result.premultipliedAlpha = 1; return result;
		}
	};
}

void vpnw::PreloadGraphics(IUnityInterfaces*) {}
void vpnw::UnloadGraphics(IUnityInterfaces*) {}
void vpnw::InitializeGraphics(IUnityInterfaces* interfaces, UnityGfxRenderer renderer)
{
	if (renderer != kUnityGfxRendererMetal) { SetError("macOS native windows require Unity Metal."); return; }
	metal = interfaces->Get<IUnityGraphicsMetal>();
	if (!metal) throw std::runtime_error("Unity's Metal native interface is unavailable.");
	device = metal->MetalDevice();
}
void vpnw::ShutdownGraphics() { pipeline = nil; device = nil; metal = nullptr; }
void vpnw::PumpPlatform() {}
std::uint32_t vpnw::PlatformCapabilities()
{
	return VPNW_CapabilityWindows | VPNW_CapabilityPlacement | VPNW_CapabilityTopmost | VPNW_CapabilityTransparency;
}
std::shared_ptr<vpnw::WindowBackend> vpnw::CreateWindowBackend(int x, int y, int width, int height)
{
	return std::make_shared<MetalWindow>(x, y, width, height);
}
