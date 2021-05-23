#include <iostream>
#include <string>
#include <vector>
#include <fstream>
#include <type_traits>

/*
comment1
comment2
*/

int main() { // another comment
	std::ifstream foo("foo");
	std::string str;
	foo >> str;
	if (str == R"(meh
muh
mah
)")
		return 1;
	else
		return 0;
}
