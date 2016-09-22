import ez_setup
ez_setup.use_setuptools()
from setuptools import setup, find_packages

setup(
    name='brightmgmnt',
    description='Provides control of running Brighter.ServiceActivator instances',
    long_description="""See the Github project page (https://github.com/iancooper/Paramore) for more on Brighter""",
    license="MIT",
    keywords="Brighter ServiceActivator Management",
    version='0.1',
    author='Ian Cooper',
    author_email='ian_hammond_cooper@yahoo.co.uk',
    url='https://github.com/iancooper/Paramore',

    packages=find_packages(),
    install_requires=['kombu>=0.3', 'docopt>=0.6', "setuptools >= 1.0"],
    data_files=[('config', ['cfg/brightmgmnt.ini'])],
    package_data={
        # If any package contains *.txt or *.rst files, include them:
        '': ['*.txt', '*.rst'],
    },
    entry_points={
        'console_scripts': [
            'run_brightmgmt = brightmgmt.__main__:run'
        ]
    },
    classifiers=[
        "Development Status :: 2 - Pre-Alpha",
        "Programming Language :: Python",
        "Programming Language :: Python :: 3.4",
        "License :: OSI Approved :: MIT License",
        "Intended Audience :: Developers",
        "Intended Audience :: Information Technology",
        "Natural Language :: English",
        "Operating System :: OS Independent",
        "Topic :: System :: Monitoring",
        "Topic :: System :: Distributed Computing",
    ]
)
