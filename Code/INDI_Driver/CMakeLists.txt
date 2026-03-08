cmake_minimum_required(VERSION 3.10)
project(indi-defocuserlite CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake_modules/")

set(CDRIVER_VERSION_MAJOR 1)
set(CDRIVER_VERSION_MINOR 0)

find_package(INDI REQUIRED)

configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/config.h.cmake
    ${CMAKE_CURRENT_BINARY_DIR}/config.h
)

configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/indi_defocuserlite.xml.cmake
    ${CMAKE_CURRENT_BINARY_DIR}/indi_defocuserlite.xml
)

include_directories(${CMAKE_CURRENT_BINARY_DIR})
include_directories(${CMAKE_CURRENT_SOURCE_DIR})
include_directories(${INDI_INCLUDE_DIR})

add_executable(indi_defocuserlite indi_defocuserlite.cpp)
target_link_libraries(indi_defocuserlite ${INDI_LIBRARIES})

install(TARGETS indi_defocuserlite RUNTIME DESTINATION bin)
install(FILES ${CMAKE_CURRENT_BINARY_DIR}/indi_defocuserlite.xml DESTINATION ${INDI_DATA_DIR})
