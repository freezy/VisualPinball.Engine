cmake_minimum_required(VERSION 3.22)
project(VpeAsio LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
# The Steinberg ASIO SDK under its GPLv3 option. Pass -DASIO_SDK_DIR=<folder containing common/ and host/> to use a local copy.
set(ASIO_SDK_DIR "" CACHE PATH "Steinberg ASIO SDK folder (ASIOSDK)")
if(NOT ASIO_SDK_DIR)
	include(FetchContent)
	FetchContent_Declare(asiosdk
		URL https://download.steinberg.net/sdk_downloads/ASIO-SDK_2.3.4_2025-10-15.zip
		URL_HASH SHA256=d5ebf0c20dd2c5f43771fd0c1418f4b361bf52434ee670097cfa6b3a335e2eca)
	FetchContent_MakeAvailable(asiosdk)
	set(ASIO_SDK_DIR ${asiosdk_SOURCE_DIR})
endif()
add_library(VpeAsio SHARED VpeAsio.cpp ${ASIO_SDK_DIR}/common/asio.cpp ${ASIO_SDK_DIR}/host/asiodrivers.cpp ${ASIO_SDK_DIR}/host/pc/asiolist.cpp)
target_include_directories(VpeAsio PRIVATE ${ASIO_SDK_DIR}/common ${ASIO_SDK_DIR}/host ${ASIO_SDK_DIR}/host/pc)
target_compile_definitions(VpeAsio PRIVATE _CRT_SECURE_NO_WARNINGS)
target_link_libraries(VpeAsio PRIVATE ole32 advapi32 user32)
# Same deployment rule as VpePcm: no VC++ redistributable needed.
set_property(TARGET VpeAsio PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
