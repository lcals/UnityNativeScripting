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
		strings_used_ = 0;
	}

	BridgeStringView CommandStream::StoreUtf8(std::string utf8)
	{
		if (strings_used_ >= strings_.size())
		{
			strings_.emplace_back(std::make_unique<std::string>());
		}

		std::string* stored = strings_[strings_used_].get();
		*stored = std::move(utf8);

		const char* p = stored->data();
		const uint32_t len = static_cast<uint32_t>(stored->size());
		strings_used_++;

		BridgeStringView view{};
		view.ptr = static_cast<uint64_t>(reinterpret_cast<uintptr_t>(p));
		view.len = len;
		return view;
	}
}
