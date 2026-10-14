// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

#define VK_USE_PLATFORM_XLIB_KHR
#include "X11Windows.h"
#include "IUnityGraphicsVulkan.h"
#include <array>
#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <stdexcept>
#include <vector>

namespace
{
	IUnityGraphicsVulkan* vulkan = nullptr;
	UnityVulkanInstance instance{};
	PFN_vkGetInstanceProcAddr loader = nullptr;
	VKAPI_ATTR VkResult VKAPI_CALL CreateInstance(const VkInstanceCreateInfo* requested, const VkAllocationCallbacks* allocator, VkInstance* result)
	{
		try
		{
			VkInstanceCreateInfo create = *requested;
			std::vector<const char*> extensions(requested->ppEnabledExtensionNames, requested->ppEnabledExtensionNames + requested->enabledExtensionCount);
			for (const char* name : { VK_KHR_SURFACE_EXTENSION_NAME, VK_KHR_XLIB_SURFACE_EXTENSION_NAME })
				if (std::none_of(extensions.begin(), extensions.end(), [&](const char* value) { return std::strcmp(value, name) == 0; })) extensions.push_back(name);
			create.enabledExtensionCount = static_cast<std::uint32_t>(extensions.size()); create.ppEnabledExtensionNames = extensions.data();
			return reinterpret_cast<PFN_vkCreateInstance>(loader(VK_NULL_HANDLE, "vkCreateInstance"))(&create, allocator, result);
		}
		catch (...) { return VK_ERROR_OUT_OF_HOST_MEMORY; }
	}
	VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL GetInstanceProcAddr(VkInstance value, const char* name)
	{
		if (!value && std::strcmp(name, "vkCreateInstance") == 0) return reinterpret_cast<PFN_vkVoidFunction>(CreateInstance);
		return loader(value, name);
	}
	PFN_vkGetInstanceProcAddr UNITY_INTERFACE_API Intercept(PFN_vkGetInstanceProcAddr original, void*) { loader = original; return GetInstanceProcAddr; }
	void Check(VkResult result, const char* operation)
	{
		if (result != VK_SUCCESS) throw std::runtime_error(std::string(operation) + ": Vulkan result " + std::to_string(result));
	}
	struct Frame
	{
		VkCommandBuffer command = VK_NULL_HANDLE;
		VkFence fence = VK_NULL_HANDLE;
		VkSemaphore acquired = VK_NULL_HANDLE;
		VkImage raw = VK_NULL_HANDLE;
		VkDeviceMemory memory = VK_NULL_HANDLE;
		VkExtent2D size{};
		VkFormat format = VK_FORMAT_UNDEFINED;
		bool initialized = false;
	};
	class VulkanWindow final : public vpnw::WindowBackend
	{
		std::mutex mutex;
		Window window = None;
		VkSurfaceKHR surface = VK_NULL_HANDLE;
		VkSwapchainKHR swapchain = VK_NULL_HANDLE;
		VkCommandPool pool = VK_NULL_HANDLE;
		std::array<Frame, 3> frames{};
		std::vector<VkImage> images;
		// Present semaphores are owned by swapchain images, so reacquisition proves their prior wait has finished.
		std::vector<VkSemaphore> presented;
		std::vector<bool> initialized;
		VkExtent2D extent{};
		VkFormat format = VK_FORMAT_UNDEFINED;
		void* texture = nullptr;
		UnityVulkanImage source{};
		bool sourceReady = false, flip = false, alive = true, visible = false, topmost = false, rebuild = true, placement = false;
		int left = 0, top = 0, width = 1, height = 1;
		std::size_t nextFrame = 0;
		std::uint64_t submitted = 0, skipped = 0;
		int lastResult = 0;

		bool FramesReady() const
		{
			for (const auto& frame : frames) if (frame.fence && vkGetFenceStatus(instance.device, frame.fence) == VK_NOT_READY) return false;
			return true;
		}
		void ReleaseSwapchain()
		{
			if (!swapchain) return;
			// This runs only on resize, retirement, or graphics shutdown, after copy fences finish.
			// Queue idling also retires presentation waits; ordinary frames never wait for GPU completion.
			VkResult idle = vkQueueWaitIdle(instance.graphicsQueue);
			if (idle != VK_ERROR_DEVICE_LOST) Check(idle, "Retire desktop presentation");
			for (auto semaphore : presented) if (semaphore) vkDestroySemaphore(instance.device, semaphore, nullptr);
			presented.clear(); images.clear(); initialized.clear();
			vkDestroySwapchainKHR(instance.device, swapchain, nullptr); swapchain = VK_NULL_HANDLE;
		}
		void BuildSwapchain()
		{
			VkSurfaceCapabilitiesKHR capabilities{};
			Check(vkGetPhysicalDeviceSurfaceCapabilitiesKHR(instance.physicalDevice, surface, &capabilities), "Query desktop surface");
			if (!(capabilities.supportedUsageFlags & VK_IMAGE_USAGE_TRANSFER_DST_BIT)) throw std::runtime_error("The desktop surface cannot receive GPU texture copies.");
			std::uint32_t count = 0;
			Check(vkGetPhysicalDeviceSurfaceFormatsKHR(instance.physicalDevice, surface, &count, nullptr), "Query desktop formats");
			std::vector<VkSurfaceFormatKHR> formats(count);
			Check(vkGetPhysicalDeviceSurfaceFormatsKHR(instance.physicalDevice, surface, &count, formats.data()), "Query desktop formats");
			VkSurfaceFormatKHR selected{ VK_FORMAT_UNDEFINED, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR };
			for (const auto& candidate : formats)
				if ((candidate.format == VK_FORMAT_B8G8R8A8_UNORM || candidate.format == VK_FORMAT_R8G8B8A8_UNORM) && candidate.colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR) { selected = candidate; break; }
			if (selected.format == VK_FORMAT_UNDEFINED) throw std::runtime_error("The desktop surface has no encoded SDR UNORM format.");
			VkCompositeAlphaFlagBitsKHR alpha = VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR;
			if (!(capabilities.supportedCompositeAlpha & alpha))
			{
				if (capabilities.supportedCompositeAlpha & VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR) alpha = VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR;
				else throw std::runtime_error("The desktop compositor cannot preserve premultiplied transparency.");
			}
			VkExtent2D size = capabilities.currentExtent;
			if (size.width == UINT32_MAX)
			{
				size.width = std::clamp(static_cast<std::uint32_t>(width), capabilities.minImageExtent.width, capabilities.maxImageExtent.width);
				size.height = std::clamp(static_cast<std::uint32_t>(height), capabilities.minImageExtent.height, capabilities.maxImageExtent.height);
			}
			std::uint32_t imageCount = capabilities.minImageCount + 1;
			if (capabilities.maxImageCount && imageCount > capabilities.maxImageCount) imageCount = capabilities.maxImageCount;
			VkPresentModeKHR mode = VK_PRESENT_MODE_FIFO_KHR;
			Check(vkGetPhysicalDeviceSurfacePresentModesKHR(instance.physicalDevice, surface, &count, nullptr), "Query presentation modes");
			std::vector<VkPresentModeKHR> modes(count);
			Check(vkGetPhysicalDeviceSurfacePresentModesKHR(instance.physicalDevice, surface, &count, modes.data()), "Query presentation modes");
			for (auto candidate : modes) if (candidate == VK_PRESENT_MODE_IMMEDIATE_KHR) mode = candidate;
			VkSwapchainCreateInfoKHR create{ VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR };
			create.surface = surface; create.minImageCount = imageCount; create.imageFormat = selected.format; create.imageColorSpace = selected.colorSpace;
			create.imageExtent = size; create.imageArrayLayers = 1; create.imageUsage = VK_IMAGE_USAGE_TRANSFER_DST_BIT;
			create.imageSharingMode = VK_SHARING_MODE_EXCLUSIVE; create.preTransform = capabilities.currentTransform;
			create.compositeAlpha = alpha; create.presentMode = mode; create.clipped = VK_TRUE;
			Check(vkCreateSwapchainKHR(instance.device, &create, nullptr, &swapchain), "Create desktop swapchain");
			Check(vkGetSwapchainImagesKHR(instance.device, swapchain, &count, nullptr), "Query swapchain images");
			images.resize(count); presented.resize(count); initialized.assign(count, false);
			Check(vkGetSwapchainImagesKHR(instance.device, swapchain, &count, images.data()), "Query swapchain images");
			VkSemaphoreCreateInfo semaphore{ VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO };
			for (auto& ready : presented) Check(vkCreateSemaphore(instance.device, &semaphore, nullptr, &ready), "Create presentation semaphore");
			extent = size; format = selected.format; rebuild = false;
		}
		VkImage EncodedSource(Frame& frame)
		{
			if (source.format != VK_FORMAT_R8G8B8A8_SRGB && source.format != VK_FORMAT_B8G8R8A8_SRGB) return source.image;
			VkFormat rawFormat = source.format == VK_FORMAT_R8G8B8A8_SRGB ? VK_FORMAT_R8G8B8A8_UNORM : VK_FORMAT_B8G8R8A8_UNORM;
			if (!frame.raw || frame.size.width != source.extent.width || frame.size.height != source.extent.height || frame.format != rawFormat)
			{
				if (frame.raw) vkDestroyImage(instance.device, frame.raw, nullptr);
				if (frame.memory) vkFreeMemory(instance.device, frame.memory, nullptr);
				frame.raw = VK_NULL_HANDLE; frame.memory = VK_NULL_HANDLE; frame.initialized = false;
				VkImageCreateInfo create{ VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO };
				create.imageType = VK_IMAGE_TYPE_2D; create.format = rawFormat; create.extent = source.extent;
				create.mipLevels = create.arrayLayers = 1; create.samples = VK_SAMPLE_COUNT_1_BIT; create.tiling = VK_IMAGE_TILING_OPTIMAL;
				create.usage = VK_IMAGE_USAGE_TRANSFER_DST_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT; create.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
				Check(vkCreateImage(instance.device, &create, nullptr, &frame.raw), "Create encoded desktop snapshot");
				VkMemoryRequirements requirements{}; vkGetImageMemoryRequirements(instance.device, frame.raw, &requirements);
				VkPhysicalDeviceMemoryProperties properties{}; vkGetPhysicalDeviceMemoryProperties(instance.physicalDevice, &properties);
				VkMemoryAllocateInfo allocation{ VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO }; allocation.allocationSize = requirements.size;
				bool selected = false;
				for (std::uint32_t i = 0; i < properties.memoryTypeCount; i++)
					if (requirements.memoryTypeBits & (1u << i))
					{
						if (!selected || (properties.memoryTypes[i].propertyFlags & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT)) allocation.memoryTypeIndex = i;
						selected = true; if (properties.memoryTypes[i].propertyFlags & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT) break;
					}
				if (!selected) throw std::runtime_error("No device memory for encoded desktop snapshot.");
				Check(vkAllocateMemory(instance.device, &allocation, nullptr, &frame.memory), "Allocate encoded desktop snapshot");
				Check(vkBindImageMemory(instance.device, frame.raw, frame.memory, 0), "Bind encoded desktop snapshot");
				frame.size = { source.extent.width, source.extent.height }; frame.format = rawFormat;
			}
			VkImageMemoryBarrier barrier{ VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER }; barrier.image = frame.raw;
			barrier.oldLayout = frame.initialized ? VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL : VK_IMAGE_LAYOUT_UNDEFINED;
			barrier.newLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.srcAccessMask = frame.initialized ? VK_ACCESS_TRANSFER_READ_BIT : 0;
			barrier.dstAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT; barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
			barrier.subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
			vkCmdPipelineBarrier(frame.command, VK_PIPELINE_STAGE_TRANSFER_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
			VkImageCopy copy{}; copy.srcSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }; copy.dstSubresource = copy.srcSubresource; copy.extent = source.extent;
			// Copy preserves raw sRGB bytes; a direct sRGB blit would decode them into linear color.
			vkCmdCopyImage(frame.command, source.image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, frame.raw, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copy);
			barrier.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.newLayout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
			barrier.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT; barrier.dstAccessMask = VK_ACCESS_TRANSFER_READ_BIT;
			vkCmdPipelineBarrier(frame.command, VK_PIPELINE_STAGE_TRANSFER_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
			frame.initialized = true; return frame.raw;
		}
	public:
		VulkanWindow(int x, int y, int w, int h) : left(x), top(y), width(w), height(h)
		{
			try
			{
			if (!vulkan || !instance.device) throw std::runtime_error("Unity Vulkan is required for Linux desktop output.");
			window = vpnw::XCreateBorderless(x, y, w, h);
			VkXlibSurfaceCreateInfoKHR create{ VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR };
			create.dpy = vpnw::XDisplay(); create.window = window;
			Check(vkCreateXlibSurfaceKHR(instance.instance, &create, nullptr, &surface), "Create X11 Vulkan surface");
			VkBool32 supported = VK_FALSE;
			Check(vkGetPhysicalDeviceSurfaceSupportKHR(instance.physicalDevice, instance.queueFamilyIndex, surface, &supported), "Query presentation queue");
			if (!supported) throw std::runtime_error("Unity's Vulkan queue cannot present to this X11 display.");
			VkCommandPoolCreateInfo poolInfo{ VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO };
			poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT; poolInfo.queueFamilyIndex = instance.queueFamilyIndex;
			Check(vkCreateCommandPool(instance.device, &poolInfo, nullptr, &pool), "Create desktop command pool");
			std::array<VkCommandBuffer, 3> buffers{};
			VkCommandBufferAllocateInfo allocation{ VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO };
			allocation.commandPool = pool; allocation.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY; allocation.commandBufferCount = 3;
			Check(vkAllocateCommandBuffers(instance.device, &allocation, buffers.data()), "Allocate desktop commands");
			VkFenceCreateInfo fence{ VK_STRUCTURE_TYPE_FENCE_CREATE_INFO }; fence.flags = VK_FENCE_CREATE_SIGNALED_BIT;
			VkSemaphoreCreateInfo semaphore{ VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO };
			for (std::size_t i = 0; i < frames.size(); i++)
			{
				frames[i].command = buffers[i];
				Check(vkCreateFence(instance.device, &fence, nullptr, &frames[i].fence), "Create desktop frame fence");
				Check(vkCreateSemaphore(instance.device, &semaphore, nullptr, &frames[i].acquired), "Create acquisition semaphore");
			}
			}
			catch (...) { Cleanup(); throw; }
		}
		~VulkanWindow() override
		{
			Cleanup();
		}
		void Cleanup() noexcept
		{
			if (swapchain) { vkQueueWaitIdle(instance.graphicsQueue); for (auto semaphore : presented) if (semaphore) vkDestroySemaphore(instance.device, semaphore, nullptr); vkDestroySwapchainKHR(instance.device, swapchain, nullptr); }
			for (const auto& frame : frames)
			{
				if (frame.fence) vkDestroyFence(instance.device, frame.fence, nullptr);
				if (frame.acquired) vkDestroySemaphore(instance.device, frame.acquired, nullptr);
				if (frame.raw) vkDestroyImage(instance.device, frame.raw, nullptr);
				if (frame.memory) vkFreeMemory(instance.device, frame.memory, nullptr);
			}
			if (pool) vkDestroyCommandPool(instance.device, pool, nullptr);
			if (surface) vkDestroySurfaceKHR(instance.instance, surface, nullptr);
			if (window) { try { vpnw::XDestroyBorderless(window); } catch (...) {} }
		}
		bool Configure(int x, int y, int w, int h, bool show, bool stayOnTop) override
		{
			std::lock_guard<std::mutex> lock(mutex);
			rebuild |= width != w || height != h; left = x; top = y; width = w; height = h; visible = show; topmost = stayOnTop;
			return vpnw::XConfigure(window, x, y, w, h, show, stayOnTop);
		}
		bool SetOrder(int order) override
		{
			std::lock_guard<std::mutex> lock(mutex); if (order >= 2) topmost = order == 2; return vpnw::XOrder(window, order);
		}
		bool SetTexture(void* value, bool flipY) override
		{
			std::lock_guard<std::mutex> lock(mutex); texture = value; flip = flipY; sourceReady = false; return true;
		}
		bool SetPlacementMode(bool enabled) override { std::lock_guard<std::mutex> lock(mutex); placement = enabled; return vpnw::XSetPlacementMode(window, enabled); }
		void Retire() override
		{
			std::lock_guard<std::mutex> lock(mutex); alive = visible = false; texture = nullptr; sourceReady = false;
			vpnw::XConfigure(window, left, top, width, height, false, topmost);
		}
		bool Collect() override
		{
			std::lock_guard<std::mutex> lock(mutex); if (lastResult == 0 && !FramesReady()) return false; ReleaseSwapchain(); return true;
		}
		void Draw() override
		{
			std::lock_guard<std::mutex> lock(mutex); sourceReady = false;
			if (!alive || !visible || !texture || !vulkan) return;
			sourceReady = vulkan->AccessTexture(texture, UnityVulkanWholeImage, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
				VK_PIPELINE_STAGE_TRANSFER_BIT, VK_ACCESS_TRANSFER_READ_BIT, kUnityVulkanResourceAccess_PipelineBarrier, &source);
			if (sourceReady && (source.samples != VK_SAMPLE_COUNT_1_BIT || source.layers != 1 || source.extent.depth != 1 ||
				(source.format != VK_FORMAT_R8G8B8A8_UNORM && source.format != VK_FORMAT_B8G8R8A8_UNORM && source.format != VK_FORMAT_R8G8B8A8_SRGB && source.format != VK_FORMAT_B8G8R8A8_SRGB)))
			{
				sourceReady = false; vpnw::SetError("Linux desktop output requires a single-layer, single-sample RGBA8/BGRA8 texture.");
			}
		}
		void Present() override
		{
			std::lock_guard<std::mutex> lock(mutex);
			VPNW_Diagnostics bounds{};
			if (alive && placement && vpnw::XDiagnostics(window, bounds)) {
				rebuild |= width != bounds.windowWidth || height != bounds.windowHeight;
				left = bounds.left; top = bounds.top; width = bounds.windowWidth; height = bounds.windowHeight;
			}
			if (!alive || !visible || !sourceReady) { skipped++; return; }
			try
			{
				if (rebuild) { if (!FramesReady()) { skipped++; return; } ReleaseSwapchain(); BuildSwapchain(); }
				auto& frame = frames[nextFrame];
				if (vkGetFenceStatus(instance.device, frame.fence) == VK_NOT_READY) { skipped++; return; }
				std::uint32_t index = 0;
				VkResult result = vkAcquireNextImageKHR(instance.device, swapchain, 0, frame.acquired, VK_NULL_HANDLE, &index);
				if (result == VK_ERROR_OUT_OF_DATE_KHR) { rebuild = true; skipped++; return; }
				if (result == VK_NOT_READY || result == VK_TIMEOUT) { skipped++; return; }
				if (result != VK_SUBOPTIMAL_KHR) Check(result, "Acquire desktop image");
				Check(vkResetCommandBuffer(frame.command, 0), "Reset desktop command");
				VkCommandBufferBeginInfo begin{ VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO }; begin.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
				Check(vkBeginCommandBuffer(frame.command, &begin), "Begin desktop copy");
				VkImageMemoryBarrier barrier{ VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER };
				barrier.oldLayout = initialized[index] ? VK_IMAGE_LAYOUT_PRESENT_SRC_KHR : VK_IMAGE_LAYOUT_UNDEFINED;
				barrier.newLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.dstAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
				barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED; barrier.image = images[index];
				barrier.subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
				vkCmdPipelineBarrier(frame.command, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
				VkImageBlit copy{}; copy.srcSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }; copy.dstSubresource = copy.srcSubresource;
				copy.srcOffsets[0] = { 0, flip ? static_cast<int>(source.extent.height) : 0, 0 };
				copy.srcOffsets[1] = { static_cast<int>(source.extent.width), flip ? 0 : static_cast<int>(source.extent.height), 1 };
				copy.dstOffsets[1] = { static_cast<int>(extent.width), static_cast<int>(extent.height), 1 };
				VkImage encoded = EncodedSource(frame);
				vkCmdBlitImage(frame.command, encoded, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, images[index], VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copy, VK_FILTER_LINEAR);
				barrier.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.newLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR;
				barrier.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT; barrier.dstAccessMask = 0;
				vkCmdPipelineBarrier(frame.command, VK_PIPELINE_STAGE_TRANSFER_BIT, VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
				Check(vkEndCommandBuffer(frame.command), "End desktop copy");
				Check(vkResetFences(instance.device, 1, &frame.fence), "Reset desktop fence");
				VkPipelineStageFlags stage = VK_PIPELINE_STAGE_TRANSFER_BIT;
				VkSubmitInfo submit{ VK_STRUCTURE_TYPE_SUBMIT_INFO };
				submit.waitSemaphoreCount = 1; submit.pWaitSemaphores = &frame.acquired; submit.pWaitDstStageMask = &stage;
				submit.commandBufferCount = 1; submit.pCommandBuffers = &frame.command; submit.signalSemaphoreCount = 1; submit.pSignalSemaphores = &presented[index];
				Check(vkQueueSubmit(instance.graphicsQueue, 1, &submit, frame.fence), "Submit desktop copy");
				VkPresentInfoKHR present{ VK_STRUCTURE_TYPE_PRESENT_INFO_KHR };
				present.waitSemaphoreCount = 1; present.pWaitSemaphores = &presented[index]; present.swapchainCount = 1; present.pSwapchains = &swapchain; present.pImageIndices = &index;
				result = vkQueuePresentKHR(instance.graphicsQueue, &present);
				if (result == VK_ERROR_OUT_OF_DATE_KHR || result == VK_SUBOPTIMAL_KHR) rebuild = true; else Check(result, "Present desktop image");
				initialized[index] = true; nextFrame = (nextFrame + 1) % frames.size(); submitted++; lastResult = 0;
			}
			catch (const std::exception& error) { lastResult = -1; alive = false; skipped++; vpnw::SetError(error.what()); }
		}
		VPNW_Diagnostics Diagnostics() override
		{
			std::lock_guard<std::mutex> lock(mutex);
			VPNW_Diagnostics result{}; result.structBytes = sizeof(result); result.graphicsRenderer = kUnityGfxRendererVulkan;
			result.deviceReady = instance.device ? 1 : 0; result.alive = alive ? 1 : 0; result.topmost = topmost ? 1 : 0;
			result.left = left; result.top = top; result.windowWidth = width; result.windowHeight = height; result.visible = visible ? 1 : 0;
			vpnw::XDiagnostics(window, result); result.sourceWidth = source.extent.width; result.sourceHeight = source.extent.height;
			result.sourceFormat = static_cast<std::uint32_t>(source.format); result.presentedFrames = submitted; result.skippedFrames = skipped; result.lastResult = lastResult; result.premultipliedAlpha = 1; return result;
		}
	};
}

void vpnw::PreloadGraphics(IUnityInterfaces* interfaces)
{
	// Retain XWayland presentation when Unity itself selects a Wayland surface.
	const char* display = std::getenv("DISPLAY");
	auto* api = interfaces->Get<IUnityGraphicsVulkanV2>();
	if (!display || !*display) return;
	// The hook only works when the library is preloaded, before Unity creates its Vulkan instance (see the .so importer).
	if (!api || !api->AddInterceptInitialization(Intercept, nullptr, 100))
		SetError("Could not register the Vulkan initialization hook; native windows need the library preloaded at startup.");
}
void vpnw::UnloadGraphics(IUnityInterfaces* interfaces)
{
	auto* api = interfaces->Get<IUnityGraphicsVulkanV2>();
	if (api) api->RemoveInterceptInitialization(Intercept);
}
void vpnw::InitializeGraphics(IUnityInterfaces* interfaces, UnityGfxRenderer renderer)
{
	if (renderer != kUnityGfxRendererVulkan) { SetError("Linux native windows require Unity Vulkan."); return; }
	vulkan = interfaces->Get<IUnityGraphicsVulkan>();
	if (!vulkan) throw std::runtime_error("Unity's Vulkan native interface is unavailable.");
	instance = vulkan->Instance();
	UnityVulkanPluginEventConfig draw{};
	draw.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
	draw.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
	draw.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission;
	vulkan->ConfigureEvent(DrawEvent, &draw);
	UnityVulkanPluginEventConfig present{};
	present.renderPassPrecondition = kUnityVulkanRenderPass_DontCare;
	present.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_Allow;
	present.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission | kUnityVulkanEventConfigFlag_FlushCommandBuffers | kUnityVulkanEventConfigFlag_SyncWorkerThreads;
	vulkan->ConfigureEvent(PresentEvent, &present);
}
void vpnw::ShutdownGraphics() { if (instance.device) vkDeviceWaitIdle(instance.device); vulkan = nullptr; }
std::shared_ptr<vpnw::WindowBackend> vpnw::CreateWindowBackend(int x, int y, int width, int height)
{
	return std::make_shared<VulkanWindow>(x, y, width, height);
}
