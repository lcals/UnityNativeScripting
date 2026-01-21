#pragma once

#include <bridge/bridge.h>

#include <cstdint>
#include <cstring>
#include <memory>
#include <string>
#include <type_traits>
#include <vector>

namespace bridge
{
	// Owns the per-frame command byte stream and any referenced string storage.
	// The stream is exposed to the Host as a raw pointer + length.
	//
	// Lifetime:
	// - Returned pointers are valid until the next Core tick clears the stream
	//   (or the core is destroyed).
	class CommandStream
	{
	public:
		void Reserve(size_t commandBytesCapacity, size_t stringCountCapacity);
		void Clear();

		// Store UTF-8 bytes and return a view that remains valid until Clear().
		BridgeStringView StoreUtf8(std::string utf8);

		uint8_t* Allocate(size_t size)
		{
			if (size == 0)
			{
				return nullptr;
			}
			const size_t oldSize = bytes_.size();
			bytes_.resize(oldSize + size);
			return bytes_.data() + oldSize;
		}

		void PushBytes(const void* data, size_t size)
		{
			if (!data || size == 0)
			{
				return;
			}
			uint8_t* dst = Allocate(size);
			std::memcpy(dst, data, size);
		}

		void PushZeroBytes(size_t size)
		{
			if (size == 0)
			{
				return;
			}
			uint8_t* dst = Allocate(size);
			std::memset(dst, 0, size);
		}

		template <class T>
		void Push(const T& command)
		{
			static_assert(std::is_trivially_copyable<T>::value, "Command must be trivially copyable");
			static_assert(std::is_standard_layout<T>::value, "Command must be standard layout");
			static_assert(sizeof(T) % 8 == 0, "Command size must be 8-byte aligned");
			static_assert(sizeof(T) <= UINT16_MAX, "Command struct too large for header.size");

			uint8_t* dst = Allocate(sizeof(T));
			std::memcpy(dst, &command, sizeof(T));
		}

		const uint8_t* Data() const
		{
			return bytes_.empty() ? nullptr : bytes_.data();
		}

		uint32_t Size() const
		{
			return static_cast<uint32_t>(bytes_.size());
		}

	private:
		std::vector<uint8_t> bytes_;
		std::vector<std::unique_ptr<std::string>> strings_;
		size_t strings_used_ = 0;
	};
}
