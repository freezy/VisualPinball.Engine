// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#include "IUnityGraphics.h"
#if !defined(__APPLE__)
#define VK_USE_PLATFORM_XLIB_KHR
#include "IUnityGraphicsVulkan.h"
#include <vulkan/vulkan.h>
#include <X11/Xutil.h>
#else
#import <Cocoa/Cocoa.h>
#import <Metal/Metal.h>
#include "IUnityGraphicsMetal.h"
#endif
#include "VisualPinballNativeWindows.h"
#include "DesktopLayout.h"
#include <atomic>
#include <chrono>
#include <cstdlib>
#include <cstring>
#include <string>
#include <iostream>
#include <stdexcept>
#include <thread>
#include <vector>

namespace
{
	std::string Error()
	{
		std::vector<char> text(static_cast<std::size_t>(VPNW_GetLastError(nullptr, 0)));
		VPNW_GetLastError(text.data(), static_cast<int>(text.size())); return text.data();
	}
	void Require(bool value, const char* message) { if (!value) throw std::runtime_error(std::string(message) + ": " + Error()); }
	IUnityGraphics graphics{};
	IUnityInterfaces interfaces{};
	bool loaded = false;
	bool gpuTested = false;
	void UNITY_INTERFACE_API Register(IUnityGraphicsDeviceEventCallback) {}
	void UNITY_INTERFACE_API Unregister(IUnityGraphicsDeviceEventCallback) {}
	int UNITY_INTERFACE_API Reserve(int) { return 49000; }

#if !defined(__APPLE__)
	IUnityGraphicsVulkan graphicsVulkan{};
	UnityVulkanInstance native{};
	UnityVulkanImage image{};
	UnityGfxRenderer UNITY_INTERFACE_API Renderer() { return kUnityGfxRendererVulkan; }
	UnityVulkanInstance UNITY_INTERFACE_API Instance() { return native; }
	void UNITY_INTERFACE_API Configure(int event, const UnityVulkanPluginEventConfig* config)
	{
		if (event == VPNW_GetPresentEventId()) Require(config->graphicsQueueAccess == kUnityVulkanGraphicsQueueAccess_Allow && (config->flags & kUnityVulkanEventConfigFlag_FlushCommandBuffers), "Presentation requires flushed queue access");
	}
	bool UNITY_INTERFACE_API Access(void* texture, const VkImageSubresource*, VkImageLayout layout, VkPipelineStageFlags, VkAccessFlags, UnityVulkanResourceAccessMode, UnityVulkanImage* output)
	{
		if (texture != &image) return false;
		*output = image; output->layout = layout; return true;
	}
	void Check(VkResult result) { if (result != VK_SUCCESS) throw std::runtime_error("Vulkan fixture: " + std::to_string(result)); }
	void StartGraphics()
	{
		const char* extensions[] = { VK_KHR_SURFACE_EXTENSION_NAME, VK_KHR_XLIB_SURFACE_EXTENSION_NAME };
		VkApplicationInfo application{ VK_STRUCTURE_TYPE_APPLICATION_INFO }; application.apiVersion = VK_API_VERSION_1_1;
		VkInstanceCreateInfo create{ VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO }; create.pApplicationInfo = &application;
		create.enabledExtensionCount = 2; create.ppEnabledExtensionNames = extensions;
		Check(vkCreateInstance(&create, nullptr, &native.instance)); std::uint32_t count = 0;
		Check(vkEnumeratePhysicalDevices(native.instance, &count, nullptr)); Require(count > 0, "A Vulkan device is required");
		std::vector<VkPhysicalDevice> devices(count); Check(vkEnumeratePhysicalDevices(native.instance, &count, devices.data())); native.physicalDevice = devices.front();
		vkGetPhysicalDeviceQueueFamilyProperties(native.physicalDevice, &count, nullptr); std::vector<VkQueueFamilyProperties> families(count);
		vkGetPhysicalDeviceQueueFamilyProperties(native.physicalDevice, &count, families.data());
		for (std::uint32_t i = 0; i < count; i++) if (families[i].queueFlags & VK_QUEUE_GRAPHICS_BIT) { native.queueFamilyIndex = i; break; }
		float priority = 1; VkDeviceQueueCreateInfo queue{ VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO };
		queue.queueFamilyIndex = native.queueFamilyIndex; queue.queueCount = 1; queue.pQueuePriorities = &priority;
		const char* deviceExtensions[] = { VK_KHR_SWAPCHAIN_EXTENSION_NAME };
		VkDeviceCreateInfo device{ VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO }; device.queueCreateInfoCount = 1; device.pQueueCreateInfos = &queue;
		device.enabledExtensionCount = 1; device.ppEnabledExtensionNames = deviceExtensions;
		Check(vkCreateDevice(native.physicalDevice, &device, nullptr, &native.device)); vkGetDeviceQueue(native.device, native.queueFamilyIndex, 0, &native.graphicsQueue);
		native.getInstanceProcAddr = vkGetInstanceProcAddr;
		graphicsVulkan.ConfigureEvent = Configure; graphicsVulkan.Instance = Instance; graphicsVulkan.AccessTexture = Access;
	}
	VkDeviceMemory memory = VK_NULL_HANDLE;
	void* Source(bool srgb = false)
	{
		UnityVulkanImage previous = image; VkDeviceMemory previousMemory = memory;
		VkImageCreateInfo create{ VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO };
		create.imageType = VK_IMAGE_TYPE_2D; create.format = srgb ? VK_FORMAT_R8G8B8A8_SRGB : VK_FORMAT_R8G8B8A8_UNORM; create.extent = { 64, 32, 1 };
		create.mipLevels = create.arrayLayers = 1; create.samples = VK_SAMPLE_COUNT_1_BIT; create.tiling = VK_IMAGE_TILING_OPTIMAL;
		create.usage = VK_IMAGE_USAGE_TRANSFER_SRC_BIT | VK_IMAGE_USAGE_TRANSFER_DST_BIT; create.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
		Check(vkCreateImage(native.device, &create, nullptr, &image.image)); VkMemoryRequirements requirement{};
		vkGetImageMemoryRequirements(native.device, image.image, &requirement); VkPhysicalDeviceMemoryProperties properties{};
		vkGetPhysicalDeviceMemoryProperties(native.physicalDevice, &properties); VkMemoryAllocateInfo allocation{ VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO };
		allocation.allocationSize = requirement.size;
		for (std::uint32_t i = 0; i < properties.memoryTypeCount; i++) if (requirement.memoryTypeBits & (1u << i)) { allocation.memoryTypeIndex = i; break; }
		Check(vkAllocateMemory(native.device, &allocation, nullptr, &memory)); Check(vkBindImageMemory(native.device, image.image, memory, 0));
		VkCommandPool pool = VK_NULL_HANDLE; VkCommandPoolCreateInfo poolInfo{ VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO }; poolInfo.queueFamilyIndex = native.queueFamilyIndex;
		Check(vkCreateCommandPool(native.device, &poolInfo, nullptr, &pool)); VkCommandBuffer command;
		VkCommandBufferAllocateInfo buffers{ VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO }; buffers.commandPool = pool; buffers.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY; buffers.commandBufferCount = 1;
		Check(vkAllocateCommandBuffers(native.device, &buffers, &command)); VkCommandBufferBeginInfo begin{ VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO }; Check(vkBeginCommandBuffer(command, &begin));
		VkImageMemoryBarrier barrier{ VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER }; barrier.image = image.image;
		barrier.oldLayout = VK_IMAGE_LAYOUT_UNDEFINED; barrier.newLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.dstAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
		barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED; barrier.subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
		vkCmdPipelineBarrier(command, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
		VkClearColorValue green{ { 0, 128.0f / 255, 0, 128.0f / 255 } };
		if (previous.image)
		{
			VkImageCopy copy{}; copy.srcSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }; copy.dstSubresource = copy.srcSubresource; copy.extent = create.extent;
			vkCmdCopyImage(command, previous.image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, image.image, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copy);
		}
		else vkCmdClearColorImage(command, image.image, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &green, 1, &barrier.subresourceRange);
		barrier.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.newLayout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
		barrier.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT; barrier.dstAccessMask = VK_ACCESS_TRANSFER_READ_BIT;
		vkCmdPipelineBarrier(command, VK_PIPELINE_STAGE_TRANSFER_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
		Check(vkEndCommandBuffer(command)); VkSubmitInfo submit{ VK_STRUCTURE_TYPE_SUBMIT_INFO }; submit.commandBufferCount = 1; submit.pCommandBuffers = &command;
		Check(vkQueueSubmit(native.graphicsQueue, 1, &submit, VK_NULL_HANDLE)); Check(vkQueueWaitIdle(native.graphicsQueue)); vkDestroyCommandPool(native.device, pool, nullptr);
		if (previous.image) { vkDestroyImage(native.device, previous.image, nullptr); vkFreeMemory(native.device, previousMemory, nullptr); }
		image.layout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL; image.extent = create.extent; image.format = create.format; image.samples = VK_SAMPLE_COUNT_1_BIT;
		image.aspect = VK_IMAGE_ASPECT_COLOR_BIT; image.usage = create.usage; image.layers = image.mipCount = 1; return &image;
	}
	void Flush() { Check(vkQueueWaitIdle(native.graphicsQueue)); }
	void StopGraphics() { vkDestroyImage(native.device, image.image, nullptr); vkFreeMemory(native.device, memory, nullptr); vkDestroyDevice(native.device, nullptr); vkDestroyInstance(native.instance, nullptr); }
#else
	IUnityGraphicsMetal graphicsMetal{};
	id<MTLDevice> device = nil; id<MTLCommandQueue> queue = nil; id<MTLCommandBuffer> command = nil; id<MTLTexture> texture = nil;
	UnityGfxRenderer UNITY_INTERFACE_API Renderer() { return kUnityGfxRendererMetal; }
	id<MTLDevice> UNITY_INTERFACE_API Device() { return device; }
	id<MTLCommandQueue> UNITY_INTERFACE_API Queue() { return queue; }
	id<MTLCommandBuffer> UNITY_INTERFACE_API Command() { return command; }
	void UNITY_INTERFACE_API EndEncoder() {}
	void StartGraphics()
	{
		[NSApplication sharedApplication]; device = MTLCreateSystemDefaultDevice(); queue = [device newCommandQueue];
		graphicsMetal.MetalDevice = Device; graphicsMetal.CurrentCommandBuffer = Command; graphicsMetal.EndCurrentCommandEncoder = EndEncoder;
	}
	void* Source(bool srgb = false)
	{
		MTLTextureDescriptor* description = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:srgb ? MTLPixelFormatRGBA8Unorm_sRGB : MTLPixelFormatRGBA8Unorm width:64 height:32 mipmapped:NO];
		description.storageMode = MTLStorageModeShared; description.usage = MTLTextureUsageShaderRead;
		texture = [device newTextureWithDescriptor:description]; std::vector<unsigned char> bytes(64 * 32 * 4);
		for (std::size_t i = 0; i < bytes.size(); i += 4) { bytes[i + 1] = bytes[i + 3] = 128; }
		[texture replaceRegion:MTLRegionMake2D(0, 0, 64, 32) mipmapLevel:0 withBytes:bytes.data() bytesPerRow:64 * 4]; return (__bridge void*)texture;
	}
	void Flush() { [command commit]; [command waitUntilCompleted]; Require(command.status != MTLCommandBufferStatusError, "Metal GPU work completed without an error"); command = nil; }
	void StopGraphics() { texture = nil; queue = nil; device = nil; }
#endif
	IUnityInterface* UNITY_INTERFACE_API Interface(UnityInterfaceGUID guid)
	{
		auto same = [&](UnityInterfaceGUID other) { return guid.m_GUIDHigh == other.m_GUIDHigh && guid.m_GUIDLow == other.m_GUIDLow; };
		if (same(GetUnityInterfaceGUID<IUnityGraphics>())) return &graphics;
#if !defined(__APPLE__)
		if (same(GetUnityInterfaceGUID<IUnityGraphicsVulkan>())) return &graphicsVulkan;
#else
		if (same(GetUnityInterfaceGUID<IUnityGraphicsMetal>())) return &graphicsMetal;
#endif
		return nullptr;
	}
	void Coordinates()
	{
		auto layout = vpnw::PhysicalLayout({ {{0, 0, 1920, 1080}, 3840, 2160}, {{-1920, 0, 1920, 1080}, 1920, 1080}, {{1920, 0, 1920, 1080}, 1920, 1080}, {{0, -1080, 1920, 1080}, 1920, 1080} });
		Require(layout[1].x == -1920 && layout[1].width == 1920, "Standard-DPI monitor left of Retina primary");
		Require(layout[2].x == 3840 && layout[2].width == 1920, "Standard-DPI monitor right of Retina primary");
		Require(layout[3].y == -1080 && layout[3].height == 1080, "Monitor above primary uses negative physical origin");
	}
#if !defined(__APPLE__)
	// Test-only lookup and readback of the plugin's own window; production code never reads pixels back.
	std::string Title(Display* connection, Window window)
	{
		char* name = nullptr; std::string result;
		if (XFetchName(connection, window, &name) && name) { result = name; XFree(name); }
		return result;
	}
	Window FindTitled(Display* connection, Window parent, const std::string& wanted, int depth)
	{
		if (depth > 16) return None;
		Window root = None, actualParent = None, *children = nullptr; unsigned int count = 0;
		if (!XQueryTree(connection, parent, &root, &actualParent, &children, &count)) return None;
		Window found = None;
		for (unsigned int i = 0; i < count && !found; i++)
			found = Title(connection, children[i]) == wanted ? children[i] : FindTitled(connection, children[i], wanted, depth + 1);
		if (children) XFree(children);
		return found;
	}
	unsigned char Channel(unsigned long pixel, unsigned long mask)
	{
		if (!mask) return 0;
		unsigned int shift = 0; while ((mask & 1) == 0) { mask >>= 1; shift++; }
		return static_cast<unsigned char>(((pixel >> shift) & mask) * 255 / mask);
	}
	// Reads the BGRA centre pixel of the plugin's window through a separate X11 connection.
	bool CenterPixel(unsigned char (&output)[4])
	{
		Display* connection = XOpenDisplay(nullptr); Require(connection != nullptr, "Open fixture X11 connection");
		bool captured = false;
		Window window = FindTitled(connection, DefaultRootWindow(connection), "Visual Pinball Window", 0);
		XWindowAttributes attributes{};
		if (window && XGetWindowAttributes(connection, window, &attributes) && attributes.map_state == IsViewable)
		{
			XImage* image = XGetImage(connection, window, attributes.width / 2, attributes.height / 2, 1, 1, AllPlanes, ZPixmap);
			if (image)
			{
				unsigned long pixel = XGetPixel(image, 0, 0);
				output[0] = Channel(pixel, image->blue_mask); output[1] = Channel(pixel, image->green_mask); output[2] = Channel(pixel, image->red_mask);
				output[3] = attributes.depth == 32 ? static_cast<unsigned char>(pixel >> 24) : 255;
				XDestroyImage(image); captured = true;
			}
		}
		XCloseDisplay(connection); return captured;
	}
#endif
	void Pixels()
	{
#if !defined(__APPLE__)
		// Some virtual X servers (Xvfb, WSLg) do not expose Vulkan-presented contents to XGetImage.
		if (const char* skip = std::getenv("VPNW_SKIP_PIXEL_CHECK"); skip && *skip == '1') { std::cout << "Pixel readback skipped (VPNW_SKIP_PIXEL_CHECK=1).\n"; return; }
		unsigned char pixel[4]{};
		for (int attempt = 0; attempt < 100; attempt++)
		{
			Require(CenterPixel(pixel), "Find the native window by title and capture its presented GPU pixels");
			if (pixel[1] != 0) break;
			std::this_thread::sleep_for(std::chrono::milliseconds(10));
		}
		const std::string observed = "GPU output preserves encoded green and alpha bytes (BGRA " + std::to_string(pixel[0]) + "," +
			std::to_string(pixel[1]) + "," + std::to_string(pixel[2]) + "," + std::to_string(pixel[3]) + ")";
		Require(pixel[0] == 0 && pixel[1] == 128 && pixel[2] == 0 && pixel[3] == 128, observed.c_str());
#endif
	}
	void Windows()
	{
		StartGraphics();
#if defined(__APPLE__)
		if (!device) { std::cout << "Metal GPU unavailable on this host; coordinate fixtures still execute.\n"; return; }
#endif
		graphics.GetRenderer = Renderer; graphics.RegisterDeviceEventCallback = Register; graphics.UnregisterDeviceEventCallback = Unregister; graphics.ReserveEventIDRange = Reserve;
		interfaces.GetInterface = Interface; UnityPluginLoad(&interfaces); loaded = true;
		const std::uint32_t capabilities = VPNW_CapabilityWindows | VPNW_CapabilityPlacement | VPNW_CapabilityTopmost | VPNW_CapabilityTransparency;
		Require(VPNW_GetCapabilities() == capabilities, "Window, placement, topmost and transparency capabilities");
		int id = VPNW_CreateWindow(-80, 24, 64, 32); Require(id > 0, "Create borderless window at negative origin");
		VPNW_Diagnostics diagnostics{}; diagnostics.structBytes = sizeof(diagnostics);
		Require(VPNW_GetDiagnostics(id, &diagnostics) && diagnostics.left == -80 && diagnostics.top == 24 && diagnostics.visible == 0, "Physical geometry and initially hidden window");
		Require(VPNW_ConfigureWindow(id, 20, 24, 96, 48, 1, 1), "Position, resize, visibility, and topmost");
		auto render = reinterpret_cast<UnityRenderingEvent>(VPNW_GetRenderEventFunc());
		for (bool srgb : { false, true })
		{
			Require(VPNW_SetTexture(id, Source(srgb), 0), "Bind native GPU texture");
			for (int frame = 0; frame < 8; frame++)
			{
#if defined(__APPLE__)
				command = [queue commandBuffer];
#endif
				render(VPNW_GetDrawEventId()); render(VPNW_GetPresentEventId()); Flush(); VPNW_Pump();
				std::this_thread::sleep_for(std::chrono::milliseconds(10));
			}
			Require(VPNW_SetPlacementMode(id, 1) && VPNW_SetPlacementMode(id, 0), "Toggle native placement interaction");
			Require(VPNW_GetDiagnostics(id, &diagnostics), "Read presentation diagnostics");
			std::cout << "srgb=" << srgb << " renderer=" << diagnostics.graphicsRenderer << " visible=" << diagnostics.visible << " dpi=" << diagnostics.dpi <<
				" rect=" << diagnostics.left << ',' << diagnostics.top << ' ' << diagnostics.windowWidth << 'x' << diagnostics.windowHeight <<
				" presents=" << diagnostics.presentedFrames << " skips=" << diagnostics.skippedFrames << " result=" << diagnostics.lastResult << " error='" << Error() << "'" << std::endl;
			Pixels();
		}
		Require(VPNW_GetDiagnostics(id, &diagnostics) && diagnostics.presentedFrames > 0 && diagnostics.premultipliedAlpha == 1, "GPU submission and premultiplied transparency");
		Require(diagnostics.sourceWidth == 64 && diagnostics.sourceHeight == 32, "Native source dimensions");
		Require(VPNW_SetZOrder(id, 0) && VPNW_SetZOrder(id, 1) && VPNW_SetZOrder(id, 3), "Back/front/topmost operations");
		Require(VPNW_SetTexture(id, nullptr, 0), "Detach native texture"); Require(VPNW_DestroyWindow(id) && VPNW_DestroyWindow(id), "Idempotent destruction");
		render(VPNW_GetPresentEventId()); Require(VPNW_GetPendingResourceCount() == 0, "Deferred GPU retirement");
		UnityPluginUnload(); loaded = false; StopGraphics();
		gpuTested = true;
	}
}

int main()
{
	try
	{
		Coordinates(); Windows();
		std::cout << "Native coordinate fixtures passed. " << (gpuTested ? "GPU window fixtures passed.\n" : "GPU window fixtures skipped.\n"); return 0;
	}
	catch (const std::exception& error) { std::cerr << error.what() << '\n'; if (loaded) { UnityPluginUnload(); StopGraphics(); } return 1; }
}
