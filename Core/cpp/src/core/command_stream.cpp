#include "command_stream.h"

namespace bridge
{
	void CommandStream::Reserve(size_t commandBytesCapacity, size_t stringCountCapacity)
	{
		bytes_.reserve(commandBytesCapacity);
		strings_.reserve(stringCountCapacity);
	}

	void CommandStream::Clear()
	{
		bytes_.clear();
		strings_.clear();
	}

	BridgeStringView CommandStream::StoreUtf8(std::string utf8)
	{
		auto stored = std::make_unique<std::string>(std::move(utf8));
		const char* p = stored->data();
		const uint32_t len = static_cast<uint32_t>(stored->size());
		strings_.emplace_back(std::move(stored));

		BridgeStringView view{};
		view.ptr = static_cast<uint64_t>(reinterpret_cast<uintptr_t>(p));
		view.len = len;
		return view;
	}

	const uint8_t* CommandStream::Data() const
	{
		return bytes_.empty() ? nullptr : bytes_.data();
	}

	uint32_t CommandStream::Size() const
	{
		return static_cast<uint32_t>(bytes_.size());
	}
}

