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

		void PushBytes(const void* data, size_t size)
		{
			if (!data || size == 0)
			{
				return;
			}
			const size_t oldSize = bytes_.size();
			bytes_.resize(oldSize + size);
			std::memcpy(bytes_.data() + oldSize, data, size);
		}

		void PushZeroBytes(size_t size)
		{
			if (size == 0)
			{
				return;
			}
			const size_t oldSize = bytes_.size();
			bytes_.resize(oldSize + size);
			std::memset(bytes_.data() + oldSize, 0, size);
		}

		template <class T>
		void Push(const T& command)
		{
			static_assert(std::is_trivially_copyable<T>::value, "Command must be trivially copyable");
			static_assert(std::is_standard_layout<T>::value, "Command must be standard layout");
			static_assert(sizeof(T) % 8 == 0, "Command size must be 8-byte aligned");
			static_assert(sizeof(T) <= UINT16_MAX, "Command struct too large for header.size");

			const size_t oldSize = bytes_.size();
			bytes_.resize(oldSize + sizeof(T));
			std::memcpy(bytes_.data() + oldSize, &command, sizeof(T));
		}

		const uint8_t* Data() const;
		uint32_t Size() const;

	private:
		std::vector<uint8_t> bytes_;
		std::vector<std::unique_ptr<std::string>> strings_;
		size_t strings_used_ = 0;
	};
}
